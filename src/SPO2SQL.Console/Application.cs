using Microsoft.Data.SqlClient;
using SPO2SQL.Configuration;
using SPO2SQL.Logging;
using SPO2SQL.Security;

namespace SPO2SQL;

public sealed class Application : BackgroundService
{
    private readonly ILogger<Application> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly SyncExitCode _exitCode;

    private readonly ApplicationOptions _appOptions;
    private readonly SharePointOptions _sharePointOptions;
    private readonly SqlOptions _sqlOptions;

    public Application(
        ILogger<Application> logger,
        ILoggerFactory loggerFactory,
        IHostApplicationLifetime lifetime,
        SyncExitCode exitCode,
        IOptions<ApplicationOptions> appOptions,
        IOptions<SharePointOptions> sharePointOptions,
        IOptions<SqlOptions> sqlOptions)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _lifetime = lifetime;
        _exitCode = exitCode;

        _appOptions = appOptions.Value;
        _sharePointOptions = sharePointOptions.Value;
        _sqlOptions = sqlOptions.Value;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        Logger.Configure(_loggerFactory.CreateLogger("SPO2SQL"));

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("═══════════════════════════════════════════════════════════");
            _logger.LogInformation("  {AppName} v{Version}", _appOptions.Name, _appOptions.Version);
            _logger.LogInformation("═══════════════════════════════════════════════════════════");
        }

        LogConfigurationSummary();

        return base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("{AppName} stopping gracefully...", _appOptions.Name);
        }
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Starting SharePoint to SQL synchronization...");

            SyncSettings settings = BuildSyncSettings();
            ConfigureLegacyBridge(settings);

            if (!settings.HasAppOnlyAuth)
            {
                _logger.LogWarning(
                    "Using legacy username/password authentication. Configure TenantId, ClientId and " +
                    "CertificatePath for app-only authentication, which is required by MFA/Azure AD " +
                    "Conditional Access policies.");
            }

            if (_appOptions.EnableHealthChecks)
            {
                await PerformHealthChecksAsync(settings, stoppingToken).ConfigureAwait(false);
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Sync configuration: DailyMode={DailyMode}, BatchSize={BatchSize}, Auth={AuthMode}, Metrics={EnableMetrics}",
                    _appOptions.DailyMode,
                    settings.BatchSize,
                    settings.HasAppOnlyAuth ? "app-only" : "username/password",
                    settings.EnableMetrics);
            }

            await RefreshSQLLists.SPOtoSQLUpdateAsync(
                settings: settings,
                daily: _appOptions.DailyMode,
                cancellationToken: stoppingToken).ConfigureAwait(false);

            _logger.LogInformation("SharePoint to SQL synchronization completed successfully");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("SharePoint to SQL synchronization was cancelled.");
            _exitCode.Fail();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Application failed with error: {Message}", ex.Message);
            _exitCode.Fail();
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }

    private void LogConfigurationSummary()
    {
        if (!_logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        _logger.LogInformation("Configuration Summary:");
        _logger.LogInformation("  Environment: {Environment}", _appOptions.Environment);
        _logger.LogInformation("  Metrics Enabled: {EnableMetrics}", _appOptions.EnableMetrics);
        _logger.LogInformation("  Health Checks Enabled: {EnableHealthChecks}", _appOptions.EnableHealthChecks);

        _logger.LogInformation("SharePoint Configuration:");
        _logger.LogInformation("  Site URL: {SiteUrl}", _sharePointOptions.SiteUrl);
        _logger.LogInformation("  Username: {Username}", MaskSensitiveData(_sharePointOptions.Username, 3));
        _logger.LogInformation("  Password: ****** (hidden)");
        _logger.LogInformation("  Timeout: {TimeoutSeconds}s", _sharePointOptions.TimeoutSeconds);
        _logger.LogInformation("  Max Retries: {MaxRetries}", _sharePointOptions.MaxRetries);
        _logger.LogInformation("  Initial Retry Delay: {InitialRetryDelayMs}ms", _sharePointOptions.InitialRetryDelayMs);

        _logger.LogInformation("SQL Server Configuration:");
        _logger.LogInformation("  Connection String: {ConnectionString}", MaskConnectionString(_sqlOptions.ConnectionString));
        _logger.LogInformation("  Command Timeout: {CommandTimeoutSeconds}s", _sqlOptions.CommandTimeoutSeconds);
        _logger.LogInformation("  Batch Size: {BatchSize}", _sqlOptions.BatchSize);
        _logger.LogInformation("  Enforce Encryption: {EnforceEncryption}", _sqlOptions.EnforceEncryption);

        _logger.LogInformation("═══════════════════════════════════════════════════════════");
    }

    private SyncSettings BuildSyncSettings() => new()
    {
        Username = _sharePointOptions.Username,
        Password = _sharePointOptions.Password,
        SiteUrl = _sharePointOptions.SiteUrl,
        SqlConnectionString = _sqlOptions.ConnectionString,
        TenantId = _sharePointOptions.TenantId,
        ClientId = _sharePointOptions.ClientId,
        CertificatePath = _sharePointOptions.CertificatePath,
        CertificatePassword = _sharePointOptions.CertificatePassword,
        CommandTimeoutSeconds = _sqlOptions.CommandTimeoutSeconds,
        BatchSize = _sqlOptions.BatchSize,
        MaxRetries = _sharePointOptions.MaxRetries,
        InitialRetryDelayMs = _sharePointOptions.InitialRetryDelayMs,
        EnableMetrics = _appOptions.EnableMetrics,
        LegacyConfigPath = _appOptions.LegacyConfigPath
    };

    private void ConfigureLegacyBridge(SyncSettings settings)
    {
        CredentialManager.RegisterFallbackProvider(
            new StaticCredentialProvider(
                settings.Username,
                settings.Password,
                settings.SqlConnectionString));
        XmlConfig.ConfigurationReader.SetSharePointBaseUrl(settings.SiteUrl);

        string configPath = settings.LegacyConfigPath ?? "";
        if (!Path.IsPathRooted(configPath))
        {
            configPath = Path.Combine(AppContext.BaseDirectory, configPath);
        }

        if (File.Exists(configPath))
        {
            XmlConfig.ConfigurationReader.SetConfigPath(configPath);
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Legacy config path set to: {ConfigPath}", configPath);
            }
        }
        else
        {
            _logger.LogWarning("Legacy UserConfig.xml not found at: {ConfigPath}", configPath);
        }
    }

    private async Task PerformHealthChecksAsync(SyncSettings settings, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Performing startup health checks...");

        ValidateSqlEncryption();

        try
        {
            using var spoUser = SPOUserFactory.Create(settings);
            var checker = new HealthChecker();

            var result = await checker.PerformHealthCheckAsync(
                spoUser,
                settings.SiteUrl,
                settings.SqlConnectionString,
                cancellationToken).ConfigureAwait(false);

            foreach (string warning in result.Warnings)
            {
                _logger.LogWarning("{HealthMessage}", warning);
            }

            foreach (string error in result.Errors)
            {
                _logger.LogError("{HealthMessage}", error);
            }

            if (!result.IsHealthy)
            {
                throw new InvalidOperationException(
                    "Startup health checks failed: " + string.Join("; ", result.Errors));
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }

        _logger.LogInformation("Health checks completed");
    }

    private void ValidateSqlEncryption()
    {
        if (!_sqlOptions.EnforceEncryption)
        {
            return;
        }

        var builder = new SqlConnectionStringBuilder(_sqlOptions.ConnectionString);
        bool encryptionEnabled = builder.ContainsKey("Encrypt") &&
            string.Equals(builder["Encrypt"]?.ToString(), "True", StringComparison.OrdinalIgnoreCase);
        bool trustsServerCertificate = builder.ContainsKey("TrustServerCertificate") &&
            string.Equals(builder["TrustServerCertificate"]?.ToString(), "True", StringComparison.OrdinalIgnoreCase);

        if (!encryptionEnabled || trustsServerCertificate)
        {
            throw new InvalidOperationException(
                "SQL encryption enforcement requires Encrypt=true and TrustServerCertificate=false.");
        }
    }

    private static string MaskSensitiveData(string value, int visibleChars = 3)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "****** (not configured)";
        }

        if (value.Length <= visibleChars)
        {
            return new string('*', value.Length);
        }

        return value[..visibleChars] + new string('*', value.Length - visibleChars);
    }

    private static string MaskConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "****** (not configured)";
        }

        var serverMatch = System.Text.RegularExpressions.Regex.Match(
            connectionString,
            @"((?:Server|Data\s*Source|Address|Addr|Network\s*Address)\s*=\s*[^;]+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (serverMatch.Success)
        {
            return $"{serverMatch.Groups[1].Value};****** (credentials hidden)";
        }

        return "****** (connection string configured)";
    }
}
