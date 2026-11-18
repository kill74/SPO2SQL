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
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SyncExitCode _exitCode;

    private readonly ApplicationOptions _appOptions;
    private readonly SharePointOptions _sharePointOptions;
    private readonly SqlOptions _sqlOptions;

    public Application(
        ILogger<Application> logger,
        ILoggerFactory loggerFactory,
        IHostApplicationLifetime lifetime,
        IHttpClientFactory httpClientFactory,
        SyncExitCode exitCode,
        IOptions<ApplicationOptions> appOptions,
        IOptions<SharePointOptions> sharePointOptions,
        IOptions<SqlOptions> sqlOptions)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _lifetime = lifetime;
        _httpClientFactory = httpClientFactory;
        _exitCode = exitCode;

        _appOptions = appOptions.Value;
        _sharePointOptions = sharePointOptions.Value;
        _sqlOptions = sqlOptions.Value;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        Logger.Configure(_loggerFactory.CreateLogger("SPO2SQL"));

        _logger.LogInformation("═══════════════════════════════════════════════════════════");
        _logger.LogInformation("  {AppName} v{Version}", _appOptions.Name, _appOptions.Version);
        _logger.LogInformation("═══════════════════════════════════════════════════════════");

        LogConfigurationSummary();

        return base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("{AppName} stopping gracefully...", _appOptions.Name);
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Starting SharePoint to SQL synchronization...");

            ConfigureLegacyBridge();

            if (_appOptions.EnableHealthChecks)
            {
                await PerformHealthChecksAsync(stoppingToken).ConfigureAwait(false);
            }

            _logger.LogInformation("Sync configuration: DailyMode={DailyMode}, BatchSize={BatchSize}",
                _appOptions.DailyMode, _sqlOptions.BatchSize);

            await Task.Run(
                () => RefreshSQLLists.SPOtoSQLUpdate(
                    daily: _appOptions.DailyMode,
                    cancellationToken: stoppingToken),
                stoppingToken).ConfigureAwait(false);

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

    private void ConfigureLegacyBridge()
    {
        CredentialManager.RegisterFallbackProvider(
            new StaticCredentialProvider(
                _sharePointOptions.Username,
                _sharePointOptions.Password,
                _sqlOptions.ConnectionString));
        XmlConfig.ConfigurationReader.SetSharePointBaseUrl(_sharePointOptions.SiteUrl);

        string configPath = _appOptions.LegacyConfigPath ?? "";
        if (!Path.IsPathRooted(configPath))
        {
            configPath = Path.Combine(AppContext.BaseDirectory, configPath);
        }

        if (File.Exists(configPath))
        {
            XmlConfig.ConfigurationReader.SetConfigPath(configPath);
            _logger.LogInformation("Legacy config path set to: {ConfigPath}", configPath);
        }
        else
        {
            _logger.LogWarning("Legacy UserConfig.xml not found at: {ConfigPath}", configPath);
        }
    }

    private async Task PerformHealthChecksAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Performing startup health checks...");

        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(_sharePointOptions.TimeoutSeconds);

            using var response = await httpClient.GetAsync(_sharePointOptions.SiteUrl, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("SharePoint URL health check: {StatusCode}", response.StatusCode);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SharePoint URL health check failed (this may be expected if authentication is required)");
        }

        if (!HasServerKeyword(_sqlOptions.ConnectionString))
        {
            _logger.LogWarning("SQL connection string does not contain a server keyword (Server=/Data Source=) - verify configuration");
        }

        ValidateSqlEncryption();

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

    private static bool HasServerKeyword(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        var match = System.Text.RegularExpressions.Regex.Match(
            connectionString,
            @"(?:Server|Data\s*Source|Address|Addr|Network\s*Address)\s*=",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success;
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
