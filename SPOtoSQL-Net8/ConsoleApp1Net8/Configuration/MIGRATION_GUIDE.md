# Configuration Migration Guide: XML ConfigHelper → IOptions\<T>

## Overview

This guide demonstrates how to migrate from the legacy XML-based `ConfigHelper` pattern to modern .NET IOptions\<T> configuration system.

## Quick Comparison

### ❌ Old Pattern (XML ConfigHelper)

```csharp
// Legacy approach - XML file reading with static methods
using Bring.XmlConfig;

// Scattered configuration access throughout the code
var (username, password) = ConfigurationReader.GetSharePointCredentials();
var connectionString = ConfigurationReader.GetSqlConnectionString();
var baseUrl = ConfigurationReader.GetSharePointBaseUrl();

// Problems:
// ✗ No validation until runtime access
// ✗ XML parsing overhead on every read
// ✗ Hard to test (static methods)
// ✗ No dependency injection support
// ✗ Configuration scattered across codebase
// ✗ Weak typing (everything is strings)
```

### ✅ New Pattern (IOptions\<T>)

```csharp
// Modern approach - Dependency injection with validation
using Bring.Configuration;
using Microsoft.Extensions.Options;

public class MyService
{
    private readonly SharePointOptions _options;
    
    public MyService(IOptions<SharePointOptions> options)
    {
        _options = options.Value; // Already validated at startup!
    }
    
    public async Task ConnectAsync()
    {
        // Configuration is strongly-typed, validated, and testable
        var client = new SharePointClient(
            _options.SiteUrl,
            _options.Username,
            _options.Password);
    }
}

// Benefits:
// ✓ Fail-fast validation at startup
// ✓ Strongly-typed with IntelliSense
// ✓ Fully testable (can inject mock options)
// ✓ Dependency injection native
// ✓ Centralized configuration
// ✓ Type-safe access
```

## Step-by-Step Migration

### Step 1: Create Configuration Classes

Define strongly-typed configuration classes with validation attributes:

```csharp
using System.ComponentModel.DataAnnotations;

public record SharePointOptions
{
    public const string SectionName = "SharePoint";
    
    [Required(ErrorMessage = "SharePoint username is required")]
    [EmailAddress(ErrorMessage = "Username must be a valid email")]
    public string Username { get; init; } = string.Empty;
    
    [Required(ErrorMessage = "SharePoint password is required")]
    public string Password { get; init; } = string.Empty;
    
    [Required(ErrorMessage = "SharePoint site URL is required")]
    [Url(ErrorMessage = "SiteUrl must be a valid URL")]
    public string SiteUrl { get; init; } = string.Empty;
    
    [Range(10, 600, ErrorMessage = "Timeout must be between 10 and 600 seconds")]
    public int TimeoutSeconds { get; init; } = 120;
}
```

### Step 2: Create Configuration File (appsettings.json)

Replace XML configuration with JSON:

```json
{
  "SharePoint": {
    "Username": "user@company.com",
    "Password": "",  // Use secrets management!
    "SiteUrl": "https://company.sharepoint.com/sites/yoursite",
    "TimeoutSeconds": 120
  },
  "Sql": {
    "ConnectionString": "",  // Use secrets management!
    "CommandTimeoutSeconds": 300,
    "BatchSize": 80
  }
}
```

### Step 3: Register Configuration in Program.cs

```csharp
var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Register and validate configuration
        services.AddOptions<SharePointOptions>()
            .Bind(context.Configuration.GetSection(SharePointOptions.SectionName))
            .ValidateDataAnnotations()  // Enable attribute validation
            .ValidateOnStart();         // Validate at startup (fail-fast)
        
        services.AddOptions<SqlOptions>()
            .Bind(context.Configuration.GetSection(SqlOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
    });
```

### Step 4: Inject Configuration into Services

```csharp
public class SharePointService
{
    private readonly SharePointOptions _options;
    private readonly ILogger<SharePointService> _logger;
    
    public SharePointService(
        IOptions<SharePointOptions> options,
        ILogger<SharePointService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }
    
    public async Task SyncDataAsync()
    {
        // Use strongly-typed configuration
        _logger.LogInformation("Connecting to {SiteUrl}", _options.SiteUrl);
        
        // Configuration is already validated - no null checks needed!
        var timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }
}
```

## Migration Examples

### Example 1: SharePoint Credentials

**Before (XML):**
```csharp
// Old XML-based approach
var (username, password) = ConfigurationReader.GetSharePointCredentials();
var client = new SharePointClient(username, password);
```

**After (IOptions):**
```csharp
// New IOptions-based approach
public class MyService(IOptions<SharePointOptions> options)
{
    private readonly SharePointOptions _options = options.Value;
    
    public void Connect()
    {
        var client = new SharePointClient(
            _options.Username, 
            _options.Password);
    }
}
```

### Example 2: SQL Connection String

**Before (XML):**
```csharp
// Old XML-based approach
var connectionString = ConfigurationReader.GetSqlConnectionString();
var connection = new SqlConnection(connectionString);
```

**After (IOptions):**
```csharp
// New IOptions-based approach
public class DataRepository(IOptions<SqlOptions> options)
{
    private readonly SqlOptions _options = options.Value;
    
    public async Task SaveAsync()
    {
        using var connection = new SqlConnection(_options.ConnectionString);
        // Connection string is already validated!
    }
}
```

### Example 3: Configuration with Nested Settings

**Before (XML):**
```csharp
// XML approach - multiple method calls
var siteUrl = GetSharePointBaseUrl();
var timeout = GetTimeout();
var maxRetries = GetMaxRetries();

// Configuration scattered across multiple XML reads
```

**After (IOptions):**
```csharp
// IOptions approach - single object
public class RetryService(IOptions<SharePointOptions> options)
{
    private readonly SharePointOptions _options = options.Value;
    
    public async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation)
    {
        var policy = Policy
            .HandleResult<T>(r => r == null)
            .WaitAndRetryAsync(
                _options.MaxRetries,
                retryAttempt => TimeSpan.FromMilliseconds(_options.InitialRetryDelayMs));
        
        return await policy.ExecuteAsync(operation);
    }
}
```

## Advanced Patterns

### Pattern 1: IOptions\<T> - Singleton (Recommended for this app)

Use when configuration doesn't change during app lifetime.

```csharp
public MyService(IOptions<SharePointOptions> options)
{
    _options = options.Value; // Cached at injection time
}
```

### Pattern 2: IOptionsSnapshot\<T> - Scoped

Use in web apps where configuration can vary per request (e.g., multi-tenant).

```csharp
public class TenantService
{
    private readonly IOptionsSnapshot<TenantOptions> _options;
    
    public TenantService(IOptionsSnapshot<TenantOptions> options)
    {
        _options = options; // Don't extract .Value yet!
    }
    
    public void ProcessRequest(string tenantId)
    {
        // Get current tenant's configuration
        var config = _options.Get(tenantId);
    }
}
```

### Pattern 3: IOptionsMonitor\<T> - Live Reload

Use when configuration can change during app lifetime (e.g., appsettings.json with reloadOnChange: true).

```csharp
public class DynamicService
{
    private readonly IOptionsMonitor<SharePointOptions> _optionsMonitor;
    
    public DynamicService(IOptionsMonitor<SharePointOptions> optionsMonitor)
    {
        _optionsMonitor = optionsMonitor;
        
        // React to configuration changes
        _optionsMonitor.OnChange(newOptions =>
        {
            Console.WriteLine($"Configuration changed! New timeout: {newOptions.TimeoutSeconds}");
        });
    }
    
    public void DoWork()
    {
        // Always get latest configuration
        var currentOptions = _optionsMonitor.CurrentValue;
    }
}
```

## Custom Validation

Add complex validation rules beyond attributes:

```csharp
// In Program.cs
services.AddOptions<SharePointOptions>()
    .Bind(context.Configuration.GetSection(SharePointOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options =>
    {
        // Custom business rules
        if (options.TimeoutSeconds < options.InitialRetryDelayMs / 1000)
        {
            return false; // Timeout must be greater than retry delay
        }
        return true;
    }, "TimeoutSeconds must be greater than InitialRetryDelayMs")
    .ValidateOnStart();
```

Or create a dedicated validator:

```csharp
public class SharePointOptionsValidator : IValidateOptions<SharePointOptions>
{
    public ValidateOptionsResult Validate(string name, SharePointOptions options)
    {
        var failures = new List<string>();
        
        // Custom validation logic
        if (!options.SiteUrl.Contains(".sharepoint.com"))
        {
            failures.Add("SiteUrl must be a valid SharePoint Online URL");
        }
        
        if (options.MaxRetries > 5 && options.InitialRetryDelayMs < 500)
        {
            failures.Add("High retry count requires longer initial delay");
        }
        
        if (failures.Any())
        {
            return ValidateOptionsResult.Fail(failures);
        }
        
        return ValidateOptionsResult.Success;
    }
}

// Register in Program.cs
services.AddSingleton<IValidateOptions<SharePointOptions>, SharePointOptionsValidator>();
```

## Secrets Management

**Never store sensitive data in appsettings.json!**

### Development: User Secrets

```bash
# Initialize user secrets
dotnet user-secrets init

# Set sensitive values
dotnet user-secrets set "SharePoint:Username" "user@company.com"
dotnet user-secrets set "SharePoint:Password" "your-password"
dotnet user-secrets set "Sql:ConnectionString" "Server=localhost;Database=SPO;..."
```

### Production: Environment Variables

```bash
# Linux/macOS
export SPO2SQL_SharePoint__Username="user@company.com"
export SPO2SQL_SharePoint__Password="your-password"
export SPO2SQL_Sql__ConnectionString="Server=prod;..."

# Windows
set SPO2SQL_SharePoint__Username=user@company.com
set SPO2SQL_SharePoint__Password=your-password
```

Note: Double underscore `__` represents nested configuration levels.

### Production: Command-Line Arguments

```bash
dotnet run --SharePoint:Username "user@company.com" \
           --SharePoint:Password "your-password" \
           --Sql:ConnectionString "Server=prod;..."
```

## Testing with IOptions

Configuration is now easily testable:

```csharp
public class SharePointServiceTests
{
    [Fact]
    public async Task ConnectAsync_ValidConfig_Succeeds()
    {
        // Arrange - Create mock configuration
        var mockOptions = Options.Create(new SharePointOptions
        {
            SiteUrl = "https://test.sharepoint.com",
            Username = "test@test.com",
            Password = "test-password",
            TimeoutSeconds = 30
        });
        
        var service = new SharePointService(mockOptions);
        
        // Act & Assert
        await service.ConnectAsync(); // No static dependencies!
    }
}
```

## Configuration Override Priority

Configuration sources are applied in this order (highest to lowest):

1. **Command-line arguments** (highest priority)
2. **Environment variables** with `SPO2SQL_` prefix
3. **User secrets** (Development only)
4. **appsettings.{Environment}.json**
5. **appsettings.json** (lowest priority)

Example: Override batch size:

```bash
# Via environment variable
export SPO2SQL_Sql__BatchSize=100

# Via command line (overrides environment)
dotnet run --Sql:BatchSize 150
```

## Logging Configuration Summary

Display configuration on startup without exposing secrets:

```csharp
private void LogConfigurationSummary()
{
    _logger.LogInformation("SharePoint Configuration:");
    _logger.LogInformation("  Site URL: {SiteUrl}", _options.SiteUrl);
    _logger.LogInformation("  Username: {Username}", MaskEmail(_options.Username));
    _logger.LogInformation("  Password: ****** (hidden)");
    _logger.LogInformation("  Timeout: {TimeoutSeconds}s", _options.TimeoutSeconds);
}

private static string MaskEmail(string email)
{
    if (string.IsNullOrWhiteSpace(email)) return "***";
    var parts = email.Split('@');
    if (parts.Length != 2) return "***";
    return $"{parts[0][..3]}***@{parts[1]}";
}
```

## Common Pitfalls

### ❌ Pitfall 1: Forgetting ValidateOnStart()

```csharp
// BAD - validation happens on first access, not startup
services.AddOptions<SharePointOptions>()
    .Bind(context.Configuration.GetSection(SharePointOptions.SectionName))
    .ValidateDataAnnotations(); // Missing .ValidateOnStart()!
```

```csharp
// GOOD - validation happens at startup (fail-fast)
services.AddOptions<SharePointOptions>()
    .Bind(context.Configuration.GetSection(SharePointOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart(); // ✓ Fails immediately if invalid
```

### ❌ Pitfall 2: Storing .Value in Field

```csharp
// BAD - stores reference to IOptions<T> (keeps wrapper overhead)
private readonly IOptions<SharePointOptions> _options;

public MyService(IOptions<SharePointOptions> options)
{
    _options = options; // Stores wrapper!
}

public void DoWork()
{
    var url = _options.Value.SiteUrl; // Extra indirection on every access
}
```

```csharp
// GOOD - extract .Value once in constructor
private readonly SharePointOptions _options;

public MyService(IOptions<SharePointOptions> options)
{
    _options = options.Value; // ✓ Extract once
}

public void DoWork()
{
    var url = _options.SiteUrl; // ✓ Direct access
}
```

### ❌ Pitfall 3: Hardcoded Secrets

```csharp
// NEVER DO THIS!
{
  "SharePoint": {
    "Username": "admin@company.com",
    "Password": "MyP@ssw0rd123"  // ❌ NEVER commit passwords!
  }
}
```

```csharp
// DO THIS:
{
  "SharePoint": {
    "Username": "",  // ✓ Leave empty, use secrets management
    "Password": ""   // ✓ Use user secrets or environment variables
  }
}
```

## Migration Checklist

- [ ] Create configuration classes with validation attributes
- [ ] Create appsettings.json with configuration sections
- [ ] Register options in Program.cs with `.ValidateDataAnnotations().ValidateOnStart()`
- [ ] Update services to inject `IOptions<T>` instead of reading XML
- [ ] Extract `.Value` once in constructor
- [ ] Move sensitive data to user secrets (dev) or environment variables (prod)
- [ ] Add configuration summary logging (without secrets)
- [ ] Remove XML configuration files and `ConfigHelper` references
- [ ] Update tests to use `Options.Create()` for mocking
- [ ] Verify startup validation catches invalid configuration

## Summary

The IOptions\<T> pattern provides:

✅ **Type Safety** - IntelliSense and compile-time checks  
✅ **Validation** - Fail-fast at startup  
✅ **Testability** - Easy to mock and test  
✅ **Flexibility** - JSON, environment variables, user secrets, command-line  
✅ **Security** - Secrets management built-in  
✅ **Performance** - No XML parsing overhead  
✅ **Maintainability** - Centralized configuration  

For more information, see:
- [Microsoft Documentation: Options pattern](https://learn.microsoft.com/en-us/dotnet/core/extensions/options)
- [Configuration in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration)
- [Safe storage of app secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)
