using SPO2SQL.Configuration;
using SPO2SQL.Models;

namespace SPO2SQL;

public class Application : IHostedService
{
    private readonly ILogger<Application> _logger;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IHttpClientFactory _httpClientFactory;

    private readonly ApplicationOptions _appOptions;
    private readonly SharePointOptions _sharePointOptions;
    private readonly SqlOptions _sqlOptions;

    public Application(
        ILogger<Application> logger,
        IHostApplicationLifetime lifetime,
        IHttpClientFactory httpClientFactory,
        IOptions<ApplicationOptions> appOptions,
        IOptions<SharePointOptions> sharePointOptions,
        IOptions<SqlOptions> sqlOptions)
    {
        _logger = logger;
        _lifetime = lifetime;
        _httpClientFactory = httpClientFactory;

        _appOptions = appOptions.Value;
        _sharePointOptions = sharePointOptions.Value;
        _sqlOptions = sqlOptions.Value;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("═══════════════════════════════════════════════════════════");
        _logger.LogInformation("  {AppName} v{Version}", _appOptions.Name, _appOptions.Version);
        _logger.LogInformation("═══════════════════════════════════════════════════════════");

        LogConfigurationSummary();

        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Application cancelled before startup.");
            _lifetime.StopApplication();
            return Task.CompletedTask;
        }

        _ = Task.Run(async () => await ExecuteAsync(cancellationToken), cancellationToken)
            .ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    _logger.LogError(t.Exception, "Application task failed");
                }
                else if (t.IsCanceled)
                {
                    _logger.LogWarning("Application task was cancelled");
                }

                _lifetime.StopApplication();
            }, TaskContinuationOptions.ExecuteSynchronously);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("{AppName} stopping gracefully...", _appOptions.Name);
        return Task.CompletedTask;
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
        _logger.LogInformation("  Password: {Password}", "****** (hidden)");
        _logger.LogInformation("  Timeout: {TimeoutSeconds}s", _sharePointOptions.TimeoutSeconds);
        _logger.LogInformation("  Max Retries: {MaxRetries}", _sharePointOptions.MaxRetries);
        _logger.LogInformation("  Initial Retry Delay: {InitialRetryDelayMs}ms", _sharePointOptions.InitialRetryDelayMs);

        _logger.LogInformation("SQL Server Configuration:");
        _logger.LogInformation("  Connection String: {ConnectionString}", MaskConnectionString(_sqlOptions.ConnectionString));
        _logger.LogInformation("  Command Timeout: {CommandTimeoutSeconds}s", _sqlOptions.CommandTimeoutSeconds);
        _logger.LogInformation("  Batch Size: {BatchSize}", _sqlOptions.BatchSize);
        _logger.LogInformation("  Enforce Encryption: {EnforceEncryption}", _sqlOptions.EnforceEncryption);

        _logger.LogInformation("═══════════════════════════════════════════════════════════");

        ValidateConfigurationInvariantsExample();
    }

    private void ValidateConfigurationInvariantsExample()
    {
        if (_sqlOptions.BatchSize < 10 || _sqlOptions.BatchSize > 1000)
        {
            throw new InvalidOperationException(
                "This should never happen! BatchSize validation should have caught this at startup.");
        }

        if (string.IsNullOrWhiteSpace(_sharePointOptions.SiteUrl))
        {
            throw new InvalidOperationException(
                "This should never happen! SiteUrl validation should have caught this at startup.");
        }

        _logger.LogDebug("Configuration validation checks passed (as expected)");
    }

    private async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting SharePoint to SQL synchronization...");

            ConfigureLegacyBridge();

            if (_appOptions.EnableHealthChecks)
            {
                await PerformHealthChecksAsync(cancellationToken);
            }

            _logger.LogInformation("Sync configuration: DailyMode={DailyMode}, BatchSize={BatchSize}",
                _appOptions.DailyMode, _sqlOptions.BatchSize);

            await Task.Run(() =>
            {
                SqlServer.RefreshSQLLists.SPOtoSQLUpdate(
                    daily: _appOptions.DailyMode,
                    cancellationToken: cancellationToken);
            }, cancellationToken);

            _logger.LogInformation("SharePoint to SQL synchronization completed successfully");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("SharePoint to SQL synchronization was cancelled.");
            Environment.ExitCode = 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Application failed with error: {Message}", ex.Message);
            Environment.ExitCode = 1;
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }

    private void ConfigureLegacyBridge()
    {
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

            using var response = await httpClient.GetAsync(_sharePointOptions.SiteUrl, cancellationToken);
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

        _logger.LogInformation("Health checks completed");
    }

    private void DemonstrateRecordModels()
    {
        _logger.LogInformation("═══════════════════════════════════════════════════════════");
        _logger.LogInformation("  Record Models Demonstration");
        _logger.LogInformation("═══════════════════════════════════════════════════════════");

        var listItem = new SharePointListItem
        {
            Id = 1,
            Title = "Q1 Sales Report",
            Created = DateTime.UtcNow.AddDays(-30),
            Author = "john.doe@contoso.com"
        };

        var updatedItem = listItem with
        {
            Title = "Q1 Sales Report - Final",
            Modified = DateTime.UtcNow
        };

        _logger.LogInformation("SharePoint Item Original: {Title}", listItem.Title);
        _logger.LogInformation("SharePoint Item Updated: {Title} (original unchanged: {Unchanged})",
            updatedItem.Title, listItem.Title == "Q1 Sales Report");

        var operation = new SyncOperation(
            Guid.NewGuid(),
            SyncType.Daily,
            DateTime.UtcNow.AddHours(-2),
            null,
            0,
            SyncStatus.Running
        );

        var (opId, syncType, start, _, _, status) = operation;
        _logger.LogInformation("Sync Operation: {Type} started at {Start}, Status: {Status}",
            syncType, start, status);

        var completedOp = operation with
        {
            EndTime = DateTime.UtcNow,
            ItemsProcessed = 1250,
            Status = SyncStatus.Completed
        };

        _logger.LogInformation("Operation completed: {Items} items in {Duration}",
            completedOp.ItemsProcessed, completedOp.Duration);

        var issues = new[]
        {
            new DataQualityIssue("CustomerList", 101, "Email", "InvalidFormat", "Missing @ symbol", Severity.High),
            new DataQualityIssue("OrderList", 202, "Total", "OutOfRange", "Negative total amount", Severity.Critical),
            new DataQualityIssue("ProductList", 303, "Description", "TooLong", "Exceeds 500 chars", Severity.Low)
        };

        foreach (var issue in issues)
        {
            var action = issue switch
            {
                { Severity: Severity.Critical } => "BLOCK SYNC",
                { Severity: Severity.High, IssueType: "InvalidFormat" } => "AUTO-FIX",
                { Severity: >= Severity.Medium } => "REVIEW",
                _ => "LOG ONLY"
            };

            _logger.LogInformation("DQ Issue: {ErrorMessage} \u2192 Action: {Action}",
                issue.ErrorMessage, action);
        }

        var stats = new SyncStatistics(
            TotalItems: 1500,
            Successful: 1450,
            Failed: 50,
            Duration: TimeSpan.FromMinutes(15)
        );

        _logger.LogInformation("Sync Statistics:");
        _logger.LogInformation("  {Summary}", stats.Summary);
        _logger.LogInformation("  Success Rate: {SuccessRate:F2}%", stats.SuccessRate);
        _logger.LogInformation("  Throughput: {Throughput:F2} items/sec", stats.Throughput);
        _logger.LogInformation("  Avg Time/Item: {AvgTime:F2}ms", stats.AverageTimePerItem.TotalMilliseconds);
        _logger.LogInformation("  Meets Quality Standards: {MeetsStandards}", stats.MeetsQualityStandards);

        var conn1 = new ConnectionInfo(
            "portal.contoso.com",
            "SharePointDB",
            "Production",
            DateTime.UtcNow.AddHours(-6)
        );

        var conn2 = new ConnectionInfo(
            "portal.contoso.com",
            "SharePointDB",
            "Production",
            conn1.LastSync
        );

        var conn3 = conn1 with { Environment = "Development" };

        _logger.LogInformation("Connection Info:");
        _logger.LogInformation("  conn1.Description: {Description}", conn1.Description);
        _logger.LogInformation("  conn1 == conn2 (value equality): {Equal}", conn1 == conn2);
        _logger.LogInformation("  ReferenceEquals(conn1, conn2): {RefEqual}", ReferenceEquals(conn1, conn2));
        _logger.LogInformation("  conn1 == conn3 (different env): {Equal}", conn1 == conn3);
        _logger.LogInformation("  Time Since Last Sync: {TimeSince:F1} hours",
            conn1.TimeSinceLastSync?.TotalHours ?? 0);

        var connections = new HashSet<ConnectionInfo> { conn1, conn2, conn3 };
        _logger.LogInformation("  HashSet with conn1, conn2, conn3: {Count} unique (conn1==conn2)",
            connections.Count);

        _logger.LogInformation("═══════════════════════════════════════════════════════════");
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
