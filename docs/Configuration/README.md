# Configuration System

This directory contains the modern IOptions\<T> configuration classes for the SharePoint Sync Tool.

## Configuration Classes

### ApplicationOptions
General application settings (name, version, environment, feature flags).

### SharePointOptions  
SharePoint Online connectivity settings with full validation:
- Username (required, email format)
- Password (required, use secrets management)
- SiteUrl (required, valid URL)
- Timeout and retry settings (range validated)

### SqlOptions
SQL Server connectivity and performance settings with validation:
- ConnectionString (required, use secrets management)
- CommandTimeout (range: 10-3600 seconds)
- BatchSize (range: 10-1000 items)
- EnforceEncryption flag

## Key Features

✅ **Fail-Fast Validation** - All configuration is validated at startup  
✅ **Strongly Typed** - IntelliSense support and compile-time safety  
✅ **Testable** - Easy to mock with `Options.Create()`  
✅ **Multiple Sources** - JSON, environment variables, user secrets, command-line  
✅ **Secure** - Supports secrets management out of the box  

## Usage Example

```csharp
// Inject IOptions<T> in your service constructor
public class MyService
{
    private readonly SharePointOptions _options;
    
    public MyService(IOptions<SharePointOptions> options)
    {
        _options = options.Value; // Already validated!
    }
    
    public async Task DoWorkAsync()
    {
        // Use strongly-typed configuration
        var client = new Client(_options.SiteUrl);
        client.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }
}
```

## Configuration Sources

Configuration is loaded from multiple sources (priority order):

1. **Command-line arguments** (highest)
2. **Environment variables** (prefix: `SPO2SQL_`)
3. **User secrets** (development only)
4. **appsettings.{Environment}.json**
5. **appsettings.json** (lowest)

## Security Best Practices

**NEVER** commit sensitive data to source control!

### Development
Use user secrets for sensitive data:

```bash
dotnet user-secrets set "SharePoint:Password" "your-password"
dotnet user-secrets set "Sql:ConnectionString" "Server=..."
```

### Production
Use environment variables:

```bash
export SPO2SQL_SharePoint__Password="your-password"
export SPO2SQL_Sql__ConnectionString="Server=..."
```

Note: Double underscore `__` represents configuration hierarchy.

## Migration Guide

See [MIGRATION_GUIDE.md](./MIGRATION_GUIDE.md) for complete details on migrating from the legacy XML ConfigHelper pattern to IOptions\<T>.

## Validation

All configuration classes use Data Annotations for validation:

- `[Required]` - Value must be provided
- `[EmailAddress]` - Must be valid email format
- `[Url]` - Must be valid URL format
- `[Range(min, max)]` - Numeric values must be within range
- `[MinLength(n)]` - String must have minimum length

Validation happens at startup thanks to:
```csharp
services.AddOptions<SharePointOptions>()
    .ValidateDataAnnotations()  // Enable attribute validation
    .ValidateOnStart();         // Validate immediately (fail-fast)
```

## Adding New Configuration

To add new configuration sections:

1. Create a new options class (e.g., `MyServiceOptions.cs`)
2. Add validation attributes
3. Register in `Program.cs`:
   ```csharp
   services.AddOptions<MyServiceOptions>()
       .Bind(context.Configuration.GetSection("MyService"))
       .ValidateDataAnnotations()
       .ValidateOnStart();
   ```
4. Add section to `appsettings.json`
5. Inject `IOptions<MyServiceOptions>` where needed

## Testing

Mock configuration in unit tests:

```csharp
var mockOptions = Options.Create(new SharePointOptions
{
    SiteUrl = "https://test.sharepoint.com",
    Username = "test@test.com",
    Password = "test-password",
    TimeoutSeconds = 30
});

var service = new MyService(mockOptions);
```

## See Also

- [Application.cs](../Application.cs) - Demonstrates IOptions\<T> patterns
- [Program.cs](../Program.cs) - Configuration registration
- [appsettings.json](../appsettings.json) - Configuration values
- [MIGRATION_GUIDE.md](./MIGRATION_GUIDE.md) - XML to IOptions migration guide
