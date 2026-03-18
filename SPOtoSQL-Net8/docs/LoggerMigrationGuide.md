# Logger Migration Guide: From Legacy Logger to ILogger<T>

## Overview

This guide helps you migrate from the legacy static `Logger` class to Microsoft's modern `ILogger<T>` interface, which is the standard logging abstraction in .NET.

---

## Key Differences

| Aspect | Legacy Logger | Modern ILogger<T> |
|--------|--------------|------------------|
| **Access Pattern** | Static class (`Logger.LogError(...)`) | Dependency injection (`_logger.LogError(...)`) |
| **Verbosity Control** | Custom integer levels (0-3) | Standard `LogLevel` enum |
| **Testability** | Difficult (static dependencies) | Easy (inject mock logger) |
| **Configuration** | Manual property setting | Configured via `appsettings.json` |
| **Providers** | Console only | Console, File, Event Log, Application Insights, etc. |
| **Type Safety** | None | Generic type parameter identifies log source |
| **Structured Logging** | No | Yes (with parameters) |
| **Scoping** | No | Yes (with `BeginScope()`) |
| **Performance** | Basic | Optimized with source generators |

---

## Verbosity Level Migration

### Legacy Verbosity Levels
```csharp
// Old: 0=silent, 1=errors, 2=warnings, 3=debug
Logger.VerboseLevel = 2;  // Show errors and warnings only
```

### Modern LogLevel Mapping
```csharp
// New: Configure in appsettings.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft": "Warning",
      "Bring": "Information"
    }
  }
}
```

**Mapping Table:**

| Legacy VerboseLevel | Modern LogLevel | What Gets Logged |
|---------------------|-----------------|------------------|
| 0 (silent) | `None` | Nothing |
| 1 (errors only) | `Error` | Errors only |
| 2 (warnings+) | `Warning` | Errors and warnings |
| 3 (debug+) | `Debug` or `Trace` | Everything including debug info |

---

## Log Level Migration Examples

### 1. Error Logging

**Before (Legacy):**
```csharp
using Bring.SPODataQuality;

try
{
    // ... some operation
}
catch (Exception ex)
{
    Logger.LogError("Failed to connect to SharePoint", ex);
}
```

**After (Modern):**
```csharp
using Microsoft.Extensions.Logging;

public class MyService
{
    private readonly ILogger<MyService> _logger;
    
    public MyService(ILogger<MyService> logger)
    {
        _logger = logger;
    }
    
    public void DoWork()
    {
        try
        {
            // ... some operation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to SharePoint");
        }
    }
}
```

**Key Improvements:**
- Exception is the first parameter (better IntelliSense)
- Automatic stack trace inclusion
- Type-safe: `ILogger<MyService>` identifies log source
- Dependency injection enables testing

---

### 2. Warning Logging

**Before (Legacy):**
```csharp
Logger.LogWarning("No items found in list 'Documents'");
```

**After (Modern):**
```csharp
_logger.LogWarning("No items found in list '{ListName}'", "Documents");
```

**Key Improvements:**
- Structured logging with named parameters
- Parameters extracted for log aggregation
- Better searchability in log management systems

---

### 3. Debug Logging

**Before (Legacy):**
```csharp
Logger.LogDebug($"Processing item {itemId} with status {status}");
```

**After (Modern):**
```csharp
_logger.LogDebug("Processing item {ItemId} with status {Status}", itemId, status);
```

**Key Improvements:**
- No string interpolation needed
- Parameters logged separately (structured logging)
- Better performance (no allocation if debug logging disabled)
- Searchable by ItemId or Status in log analytics

---

### 4. Information Logging (New!)

The legacy Logger didn't have an info level. Use this for important operational messages:

```csharp
_logger.LogInformation("SharePoint sync started at {StartTime}", DateTime.Now);
_logger.LogInformation("Processed {ItemCount} items in {Duration}ms", count, elapsed);
```

---

## Common Migration Patterns

### Pattern 1: Static Logger Usage Throughout Class

**Before:**
```csharp
public class SharePointService
{
    public void DownloadFiles()
    {
        Logger.LogDebug("Starting file download");
        
        try
        {
            // ... download logic
            Logger.LogDebug("Downloaded 5 files successfully");
        }
        catch (Exception ex)
        {
            Logger.LogError("Download failed", ex);
        }
    }
}
```

**After:**
```csharp
public class SharePointService
{
    private readonly ILogger<SharePointService> _logger;
    
    public SharePointService(ILogger<SharePointService> logger)
    {
        _logger = logger;
    }
    
    public void DownloadFiles()
    {
        _logger.LogDebug("Starting file download");
        
        try
        {
            // ... download logic
            _logger.LogInformation("Downloaded {FileCount} files successfully", 5);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Download failed");
        }
    }
}
```

---

### Pattern 2: Verbosity Level Checking

**Before:**
```csharp
if (Logger.VerboseLevel >= 3)
{
    Logger.LogDebug($"Details: {expensiveOperation()}");
}
```

**After:**
```csharp
// Option 1: Let ILogger handle it (recommended)
_logger.LogDebug("Details: {Details}", expensiveOperation());

// Option 2: Explicit check for expensive operations
if (_logger.IsEnabled(LogLevel.Debug))
{
    _logger.LogDebug("Details: {Details}", expensiveOperation());
}
```

**Note:** The second approach only necessary if `expensiveOperation()` is truly expensive, as ILogger optimizes away disabled log levels.

---

### Pattern 3: Custom Log Level Method

**Before:**
```csharp
Logger.Log(2, "Custom warning message");
```

**After:**
```csharp
_logger.Log(LogLevel.Warning, "Custom warning message");

// Or use the specific helper:
_logger.LogWarning("Custom warning message");
```

---

## Using the LoggerAdapter (Transition Strategy)

If you have a large codebase and can't migrate everything at once, use the `LoggerAdapter`:

```csharp
using Bring.Adapters;
using Microsoft.Extensions.Logging;

public class LegacyService
{
    public LegacyService(ILogger<LegacyService> modernLogger)
    {
        // Wrap modern logger to support legacy Logger API
        var adapter = new LoggerAdapter(modernLogger);
        
        // Configure legacy verbosity level
        adapter.VerboseLevel = 3;
        
        // Now legacy code can work unchanged
        adapter.LogError("Legacy error message");
        adapter.LogWarning("Legacy warning message");
        adapter.LogDebug("Legacy debug message");
    }
}
```

**When to use the adapter:**
- Large codebase with many Logger references
- Gradual migration strategy
- Testing migration incrementally
- Supporting legacy code that can't be changed immediately

**When NOT to use the adapter:**
- New code (use ILogger<T> directly)
- Small codebases (just migrate directly)
- When you need structured logging features

---

## Configuration Migration

### Legacy Configuration
```csharp
// Somewhere in Program.cs or startup
Logger.VerboseLevel = args.Contains("--verbose") ? 3 : 1;
```

### Modern Configuration

**appsettings.json:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information",
      "Bring": "Debug"
    },
    "Console": {
      "FormatterName": "simple",
      "FormatterOptions": {
        "TimestampFormat": "yyyy-MM-dd HH:mm:ss.fff ",
        "SingleLine": true,
        "IncludeScopes": false
      }
    }
  }
}
```

**appsettings.Development.json:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Bring": "Trace"
    }
  }
}
```

**Command-line override:**
```bash
dotnet run --Logging:LogLevel:Default=Debug
```

---

## Best Practices

### ✅ DO

1. **Use structured logging:**
   ```csharp
   _logger.LogInformation("User {UserId} logged in from {IpAddress}", userId, ip);
   ```

2. **Pass exceptions as first parameter:**
   ```csharp
   _logger.LogError(ex, "Failed to process {FileName}", fileName);
   ```

3. **Use appropriate log levels:**
   - `Trace`: Very detailed (e.g., entering/exiting methods)
   - `Debug`: Developer information during debugging
   - `Information`: Important business events
   - `Warning`: Recoverable errors or unexpected situations
   - `Error`: Failures that stop current operation
   - `Critical`: Catastrophic failures requiring immediate attention

4. **Check log level for expensive operations:**
   ```csharp
   if (_logger.IsEnabled(LogLevel.Debug))
   {
       _logger.LogDebug("Complex data: {Data}", SerializeComplexObject());
   }
   ```

### ❌ DON'T

1. **Don't use string interpolation:**
   ```csharp
   // BAD - loses structured logging
   _logger.LogInformation($"Processing {itemId}");
   
   // GOOD - structured logging
   _logger.LogInformation("Processing {ItemId}", itemId);
   ```

2. **Don't catch exceptions just to log them:**
   ```csharp
   // BAD
   try { DoWork(); }
   catch (Exception ex) { _logger.LogError(ex, "Error"); throw; }
   
   // GOOD - let exceptions bubble, log at appropriate handler level
   ```

3. **Don't log sensitive data:**
   ```csharp
   // BAD
   _logger.LogDebug("Password: {Password}", password);
   
   // GOOD
   _logger.LogDebug("Authentication attempted for user {Username}", username);
   ```

---

## Testing with ILogger

### Legacy Logger (Hard to Test)
```csharp
public void TestMethod()
{
    // No way to verify logging without capturing Console.Out
    var service = new SharePointService();
    service.DoWork();
    // Can't assert what was logged
}
```

### Modern ILogger (Easy to Test)
```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class SharePointServiceTests
{
    [Fact]
    public void DoWork_LogsInformation()
    {
        // Arrange
        var logger = new FakeLogger<SharePointService>();
        var service = new SharePointService(logger);
        
        // Act
        service.DoWork();
        
        // Assert
        Assert.Contains(logger.LoggedMessages, 
            m => m.Level == LogLevel.Information && m.Message.Contains("Downloaded"));
    }
    
    [Fact]
    public void DoWork_WithNullLogger_StillWorks()
    {
        // NullLogger for tests where logging doesn't matter
        var service = new SharePointService(NullLogger<SharePointService>.Instance);
        service.DoWork(); // No exceptions
    }
}
```

---

## Migration Checklist

- [ ] Update all service constructors to accept `ILogger<T>`
- [ ] Replace `Logger.LogError()` with `_logger.LogError()`
- [ ] Replace `Logger.LogWarning()` with `_logger.LogWarning()`
- [ ] Replace `Logger.LogDebug()` with `_logger.LogDebug()`
- [ ] Replace `Logger.Log()` with `_logger.Log()`
- [ ] Convert string interpolation to structured logging parameters
- [ ] Update exception logging to pass exception as first parameter
- [ ] Configure logging in `appsettings.json` instead of `Logger.VerboseLevel`
- [ ] Update unit tests to inject logger mocks
- [ ] Remove `using Bring.SPODataQuality;` (legacy namespace)
- [ ] Add `using Microsoft.Extensions.Logging;`
- [ ] Consider using `LoggerAdapter` for gradual migration

---

## Additional Resources

- [Microsoft Logging Documentation](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging)
- [High-performance logging](https://learn.microsoft.com/en-us/dotnet/core/extensions/high-performance-logging)
- [Logging Best Practices](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging-best-practices)
- Project: `Application.cs` - Example of modern ILogger<T> usage

---

## Questions?

If you encounter issues during migration, check:
1. Is the service registered in DI? (see `Program.cs`)
2. Is logging configured in `appsettings.json`?
3. Are you using `ILogger<T>` where T is your class name?
4. Have you removed static `Logger` references?

For gradual migration, use the `LoggerAdapter` class in `ConsoleApp1Net8/Adapters/LoggerAdapter.cs`.
