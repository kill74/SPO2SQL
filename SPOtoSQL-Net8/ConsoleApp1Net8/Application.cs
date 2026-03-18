using Bring.Configuration;
using Bring.Models;

namespace Bring;

/// <summary>
/// Main application logic as a hosted service.
/// Demonstrates modern IOptions&lt;T&gt; configuration patterns for .NET 8.
/// </summary>
/// <remarks>
/// <para><strong>Migration from XML ConfigHelper to IOptions&lt;T&gt;</strong></para>
/// <para>Old XML Pattern:</para>
/// <code>
/// // XML ConfigHelper Pattern (Legacy)
/// var (username, password) = ConfigurationReader.GetSharePointCredentials();
/// var connectionString = ConfigurationReader.GetSqlConnectionString();
/// var baseUrl = ConfigurationReader.GetSharePointBaseUrl();
/// 
/// // Problems with this approach:
/// // - No validation until runtime access
/// // - Scattered configuration access throughout codebase
/// // - Hard to test (static methods)
/// // - No dependency injection support
/// // - XML parsing overhead on every read
/// </code>
/// <para>New IOptions Pattern (Modern):</para>
/// <code>
/// // IOptions Pattern - validated on startup, injected via DI
/// public MyService(IOptions&lt;SharePointOptions&gt; sharePointOptions)
/// {
///     _sharePointOptions = sharePointOptions.Value; // Already validated!
/// }
/// 
/// // Benefits:
/// // - Validation happens at startup (fail-fast)
/// // - Configuration is strongly-typed
/// // - Fully testable (can inject mock options)
/// // - Dependency injection native
/// // - Type-safe access with IntelliSense
/// // - Supports JSON, environment variables, user secrets, and command-line args
/// </code>
/// </remarks>
public class Application : IHostedService
{
    private readonly ILogger<Application> _logger;
    private readonly IHostApplicationLifetime _lifetime;
    
    // Pattern 1: IOptions<T> - Singleton pattern, value cached at injection time
    // Best for: Configuration that doesn't change during app lifetime
    private readonly ApplicationOptions _appOptions;
    private readonly SharePointOptions _sharePointOptions;
    private readonly SqlOptions _sqlOptions;
    
    // Pattern 2: IOptionsMonitor<T> - Supports live configuration reloading
    // Best for: Configuration that can change during app lifetime (e.g., appsettings.json with reloadOnChange: true)
    // Not shown here as this app doesn't need live reload, but available if needed
    
    // Pattern 3: IOptionsSnapshot<T> - Scoped lifetime, recomputed per request
    // Best for: Multi-tenant scenarios or when configuration varies per operation
    // Not applicable for this console app, but useful in web APIs

    public Application(
        ILogger<Application> logger,
        IHostApplicationLifetime lifetime,
        IOptions<ApplicationOptions> appOptions,
        IOptions<SharePointOptions> sharePointOptions,
        IOptions<SqlOptions> sqlOptions)
    {
        _logger = logger;
        _lifetime = lifetime;
        
        // Extract .Value once in constructor - this is already validated by ValidateOnStart()
        // If validation failed, we wouldn't reach this point (app would fail to start)
        _appOptions = appOptions.Value;
        _sharePointOptions = sharePointOptions.Value;
        _sqlOptions = sqlOptions.Value;
        
        // Configuration is guaranteed to be valid here thanks to:
        // 1. [Required] and [Range] attributes on option classes
        // 2. .ValidateDataAnnotations() in Program.cs
        // 3. .ValidateOnStart() in Program.cs
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("═══════════════════════════════════════════════════════════");
        _logger.LogInformation("  {AppName} v{Version}", _appOptions.Name, _appOptions.Version);
        _logger.LogInformation("═══════════════════════════════════════════════════════════");
        
        // Display configuration summary (without sensitive data)
        LogConfigurationSummary();

        // Start the actual work in a background task
        _ = Task.Run(async () => await ExecuteAsync(cancellationToken), cancellationToken);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Logs a comprehensive configuration summary on startup.
    /// Demonstrates accessing configuration values safely without exposing sensitive data.
    /// </summary>
    private void LogConfigurationSummary()
    {
        _logger.LogInformation("Configuration Summary:");
        _logger.LogInformation("  Environment: {Environment}", _appOptions.Environment);
        _logger.LogInformation("  Metrics Enabled: {EnableMetrics}", _appOptions.EnableMetrics);
        _logger.LogInformation("  Health Checks Enabled: {EnableHealthChecks}", _appOptions.EnableHealthChecks);
        
        // SharePoint configuration (hide sensitive data)
        _logger.LogInformation("SharePoint Configuration:");
        _logger.LogInformation("  Site URL: {SiteUrl}", _sharePointOptions.SiteUrl);
        _logger.LogInformation("  Username: {Username}", MaskSensitiveData(_sharePointOptions.Username, 3));
        _logger.LogInformation("  Password: {Password}", "****** (hidden)");
        _logger.LogInformation("  Timeout: {TimeoutSeconds}s", _sharePointOptions.TimeoutSeconds);
        _logger.LogInformation("  Max Retries: {MaxRetries}", _sharePointOptions.MaxRetries);
        _logger.LogInformation("  Initial Retry Delay: {InitialRetryDelayMs}ms", _sharePointOptions.InitialRetryDelayMs);
        
        // SQL configuration (hide connection string)
        _logger.LogInformation("SQL Server Configuration:");
        _logger.LogInformation("  Connection String: {ConnectionString}", MaskConnectionString(_sqlOptions.ConnectionString));
        _logger.LogInformation("  Command Timeout: {CommandTimeoutSeconds}s", _sqlOptions.CommandTimeoutSeconds);
        _logger.LogInformation("  Batch Size: {BatchSize}", _sqlOptions.BatchSize);
        _logger.LogInformation("  Enforce Encryption: {EnforceEncryption}", _sqlOptions.EnforceEncryption);
        
        _logger.LogInformation("═══════════════════════════════════════════════════════════");
        
        // Example: Demonstrating validation - these would throw if values were invalid
        // But we know they're valid because ValidateOnStart() already checked them
        ValidateConfigurationInvariantsExample();
    }

    /// <summary>
    /// Demonstrates how validated configuration prevents runtime errors.
    /// These checks are redundant (already validated at startup), but shown for educational purposes.
    /// </summary>
    private void ValidateConfigurationInvariantsExample()
    {
        // Example 1: Range validation - BatchSize must be 10-1000
        // This is guaranteed by [Range(10, 1000)] attribute on SqlOptions.BatchSize
        if (_sqlOptions.BatchSize < 10 || _sqlOptions.BatchSize > 1000)
        {
            throw new InvalidOperationException(
                "This should never happen! BatchSize validation should have caught this at startup.");
        }
        
        // Example 2: Required validation - SiteUrl must not be empty
        // This is guaranteed by [Required] and [Url] attributes on SharePointOptions.SiteUrl
        if (string.IsNullOrWhiteSpace(_sharePointOptions.SiteUrl))
        {
            throw new InvalidOperationException(
                "This should never happen! SiteUrl validation should have caught this at startup.");
        }
        
        // Example 3: Custom business rule - could add custom validation in Program.cs
        // e.g., ValidateOptionsResult.Fail("TimeoutSeconds must be greater than InitialRetryDelayMs")
        
        _logger.LogDebug("Configuration validation checks passed (as expected)");
    }

    private async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Clean access to configuration values - no parsing, no validation needed here
            // Everything is strongly-typed and already validated
            
            _logger.LogInformation("Starting SharePoint to SQL synchronization...");
            
            // Example: Accessing nested configuration values in a clean way
            var retryConfig = new
            {
                MaxAttempts = _sharePointOptions.MaxRetries,
                InitialDelay = TimeSpan.FromMilliseconds(_sharePointOptions.InitialRetryDelayMs),
                Timeout = TimeSpan.FromSeconds(_sharePointOptions.TimeoutSeconds)
            };
            
            _logger.LogDebug("Retry configuration: Max attempts={MaxAttempts}, Initial delay={InitialDelay}, Timeout={Timeout}",
                retryConfig.MaxAttempts,
                retryConfig.InitialDelay,
                retryConfig.Timeout);

            // Example: Using configuration values for business logic
            if (_appOptions.EnableHealthChecks)
            {
                await PerformHealthChecksAsync(cancellationToken);
            }

            if (_appOptions.EnableMetrics)
            {
                _logger.LogInformation("Metrics collection is enabled");
                // Initialize metrics collection here
            }

            // TODO: Implement the actual sync logic
            // This will be populated as we modernize the core services
            // Example services would receive IOptions<T> via constructor injection:
            //
            // public class SharePointService(IOptions<SharePointOptions> options)
            // {
            //     private readonly SharePointOptions _options = options.Value;
            //     
            //     public async Task ConnectAsync()
            //     {
            //         // Use _options.SiteUrl, _options.Username, etc.
            //     }
            // }

            // Demonstrate record-based DTOs
            DemonstrateRecordModels();

            _logger.LogInformation("Application completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Application failed with error: {Message}", ex.Message);
            Environment.ExitCode = 1;
        }
        finally
        {
            // Stop the application
            _lifetime.StopApplication();
        }
    }

    /// <summary>
    /// Example health check using configuration values.
    /// </summary>
    private async Task PerformHealthChecksAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Performing startup health checks...");
        
        // Example: Verify SharePoint URL is accessible
        try
        {
            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(_sharePointOptions.TimeoutSeconds)
            };
            
            var response = await httpClient.GetAsync(_sharePointOptions.SiteUrl, cancellationToken);
            _logger.LogInformation("SharePoint URL health check: {StatusCode}", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SharePoint URL health check failed (this may be expected if authentication is required)");
        }
        
        // Example: Verify SQL connection string format
        if (!_sqlOptions.ConnectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("SQL connection string does not contain 'Server=' - verify configuration");
        }
        
        _logger.LogInformation("Health checks completed");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("{AppName} stopping gracefully...", _appOptions.Name);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Demonstrates record-based data models with immutability, value equality, and pattern matching.
    /// </summary>
    private void DemonstrateRecordModels()
    {
        _logger.LogInformation("═══════════════════════════════════════════════════════════");
        _logger.LogInformation("  Record Models Demonstration");
        _logger.LogInformation("═══════════════════════════════════════════════════════════");

        // 1. SharePointListItem - Init-only properties with with-expressions
        var listItem = new SharePointListItem
        {
            Id = 1,
            Title = "Q1 Sales Report",
            Created = DateTime.UtcNow.AddDays(-30),
            Author = "john.doe@contoso.com"
        };

        // Non-destructive mutation with with-expression
        var updatedItem = listItem with 
        { 
            Title = "Q1 Sales Report - Final",
            Modified = DateTime.UtcNow
        };

        _logger.LogInformation("SharePoint Item Original: {Title}", listItem.Title);
        _logger.LogInformation("SharePoint Item Updated: {Title} (original unchanged: {Unchanged})", 
            updatedItem.Title, listItem.Title == "Q1 Sales Report");

        // 2. SyncOperation - Positional record with deconstruction
        var operation = new SyncOperation(
            Guid.NewGuid(),
            SyncType.Daily,
            DateTime.UtcNow.AddHours(-2),
            null,
            0,
            "Running"
        );

        // Deconstruction
        var (opId, syncType, start, _, _, status) = operation;
        _logger.LogInformation("Sync Operation: {Type} started at {Start}, Status: {Status}", 
            syncType, start, status);

        // Update operation when complete
        var completedOp = operation with
        {
            EndTime = DateTime.UtcNow,
            ItemsProcessed = 1250,
            Status = "Completed"
        };

        _logger.LogInformation("Operation completed: {Items} items in {Duration}", 
            completedOp.ItemsProcessed, completedOp.Duration);

        // 3. DataQualityIssue - Pattern matching
        var issues = new[]
        {
            new DataQualityIssue("CustomerList", 101, "Email", "InvalidFormat", "Missing @ symbol", Severity.High),
            new DataQualityIssue("OrderList", 202, "Total", "OutOfRange", "Negative total amount", Severity.Critical),
            new DataQualityIssue("ProductList", 303, "Description", "TooLong", "Exceeds 500 chars", Severity.Low)
        };

        foreach (var issue in issues)
        {
            // Property pattern matching
            var action = issue switch
            {
                { Severity: Severity.Critical } => "BLOCK SYNC",
                { Severity: Severity.High, IssueType: "InvalidFormat" } => "AUTO-FIX",
                { Severity: >= Severity.Medium } => "REVIEW",
                _ => "LOG ONLY"
            };

            _logger.LogInformation("DQ Issue: {ErrorMessage} → Action: {Action}", 
                issue.ErrorMessage, action);
        }

        // 4. SyncStatistics - Calculated properties
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

        // 5. ConnectionInfo - Value equality
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
            conn1.LastSync // Same timestamp
        );

        var conn3 = conn1 with { Environment = "Development" };

        _logger.LogInformation("Connection Info:");
        _logger.LogInformation("  conn1.Description: {Description}", conn1.Description);
        _logger.LogInformation("  conn1 == conn2 (value equality): {Equal}", conn1 == conn2);
        _logger.LogInformation("  ReferenceEquals(conn1, conn2): {RefEqual}", ReferenceEquals(conn1, conn2));
        _logger.LogInformation("  conn1 == conn3 (different env): {Equal}", conn1 == conn3);
        _logger.LogInformation("  Time Since Last Sync: {TimeSince:F1} hours", 
            conn1.TimeSinceLastSync?.TotalHours ?? 0);

        // Demonstrate value equality in collections
        var connections = new HashSet<ConnectionInfo> { conn1, conn2, conn3 };
        _logger.LogInformation("  HashSet with conn1, conn2, conn3: {Count} unique (conn1==conn2)", 
            connections.Count);

        _logger.LogInformation("═══════════════════════════════════════════════════════════");
    }

    /// <summary>
    /// Masks sensitive data for logging, showing only the first N characters.
    /// </summary>
    /// <param name="value">The sensitive value to mask.</param>
    /// <param name="visibleChars">Number of characters to show before masking.</param>
    /// <returns>Masked string (e.g., "use***").</returns>
    private static string MaskSensitiveData(string value, int visibleChars = 3)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "****** (not configured)";
        
        if (value.Length <= visibleChars)
            return new string('*', value.Length);
        
        return value[..visibleChars] + new string('*', Math.Min(6, value.Length - visibleChars));
    }

    /// <summary>
    /// Masks connection string for logging, showing only server name.
    /// </summary>
    /// <param name="connectionString">The connection string to mask.</param>
    /// <returns>Masked connection string showing only server.</returns>
    private static string MaskConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return "****** (not configured)";
        
        // Extract server name for logging (simple approach)
        var serverMatch = System.Text.RegularExpressions.Regex.Match(
            connectionString, 
            @"Server=([^;]+)", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        if (serverMatch.Success)
            return $"Server={serverMatch.Groups[1].Value};****** (credentials hidden)";
        
        return "****** (connection string configured)";
    }
}
