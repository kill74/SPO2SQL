# .NET 8 Modernization Guide

This document explains the modern .NET 8 architecture and how to configure and run the modernized SharePoint Sync Tool.

## What's New in v2.0

### Modern Architecture
- **Dependency Injection**: Full DI container with IHost pattern
- **Async/Await**: All SharePoint, SQL and data-quality I/O uses async patterns with cancellation
- **Structured Logging**: ILogger<T> in the host, bridged to the Core library
- **Configuration**: appsettings.json with validated IOptions<T>, mapped to a single `SyncSettings` record
- **Security**: User Secrets (dev), environment variables and app-only certificate authentication (prod)
- **Resilience**: Cancellation-aware retry with exponential backoff and jitter
- **Testing**: xUnit unit tests; integration tests require a live tenant/SQL Server

### Modern C# Features
- Records for immutable configuration
- File-scoped namespaces
- Global usings
- Nullable reference types enabled
- Pattern matching
- Top-level statements in Program.cs

## Configuration

### Development Setup (User Secrets)

**Quick Setup**:
```bash
# Stores credentials in the current user's DPAPI-protected store (Windows)
dotnet run --project src/SPO2SQL.Console/SPO2SQL.Console.csproj -- --setup-secrets
```

For development, user secrets remain the preferred option; see the manual setup below.

**Manual Setup**:

1. **Initialize User Secrets** (one-time setup):
```bash
cd src/SPO2SQL.Console
dotnet user-secrets init
```

2. **Set Your Credentials**:
```bash
dotnet user-secrets set "SharePoint:Username" "your-email@company.com"
dotnet user-secrets set "SharePoint:Password" "your-password"
dotnet user-secrets set "SharePoint:SiteUrl" "https://yourcompany.sharepoint.com/sites/yoursite"
dotnet user-secrets set "Sql:ConnectionString" "Server=myServer;Database=myDB;Integrated Security=true;"
```

3. **Verify Configuration**:
```bash
# List secrets (passwords are masked)
dotnet user-secrets list

# Or use the verification script
./scripts/verify-config.sh
```

### Production Setup (Environment Variables)

Set environment variables with the `SPO2SQL_` prefix:

**Windows (PowerShell)**:
```powershell
$env:SPO2SQL_SharePoint__Username="your-email@company.com"
$env:SPO2SQL_SharePoint__Password="your-password"
$env:SPO2SQL_SharePoint__SiteUrl="https://yourcompany.sharepoint.com/sites/yoursite"
$env:SPO2SQL_Sql__ConnectionString="Server=myServer;Database=myDB;Integrated Security=true;"
```

**Linux/Mac**:
```bash
export SPO2SQL_SharePoint__Username="your-email@company.com"
export SPO2SQL_SharePoint__Password="your-password"
export SPO2SQL_SharePoint__SiteUrl="https://yourcompany.sharepoint.com/sites/yoursite"
export SPO2SQL_Sql__ConnectionString="Server=myServer;Database=myDB;Integrated Security=true;"
```

**Note**: Use double underscores `__` to represent nested configuration sections.

### Configuration Files

**appsettings.json** - Default settings:
- Application behavior
- Logging configuration
- Default timeouts and batch sizes
- Does NOT contain credentials

**appsettings.Development.json** - Development overrides:
- Debug logging
- Development-specific settings

**User Secrets** (development) or **Environment Variables** (production):
- SharePoint credentials
- SQL connection strings
- Sensitive data

## Running the Application

### Development
```bash
cd src/SPO2SQL.Console
dotnet restore
dotnet build
dotnet run
```

### Production
```bash
dotnet publish -c Release -r win-x64 --self-contained false
cd bin/Release/net8.0/win-x64/publish
dotnet run --project src/SPO2SQL.Console/SPO2SQL.Console.csproj
```

## Logging

Configure logging in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "System": "Warning"
    }
  }
}
```

Available log levels:
- `Trace` - Most detailed (use for debugging)
- `Debug` - Detailed information
- `Information` - General flow (default)
- `Warning` - Unexpected events
- `Error` - Failures
- `Critical` - Catastrophic failures

## Migration from .NET Framework Version

### Quick Migration

1. **Export Current XML Config**:
   - Copy values from `XmlConfig/UserConfig.xml`
   
2. **Set User Secrets**:
   ```bash
   dotnet user-secrets set "SharePoint:Username" "[from XML]"
   dotnet user-secrets set "SharePoint:Password" "[from XML]"
   dotnet user-secrets set "Sql:ConnectionString" "[from XML]"
   ```

3. **Test the Application**:
   ```bash
   dotnet run
   ```

### Side-by-Side Operation

Both versions can coexist:
- **.NET Framework 4.8**: `SPOtoSQL-Snapshots/ConsoleApp1/`
- **.NET 8**: `src/SPO2SQL.Console/`

The .NET 8 version links to the same source files for now, so changes are shared.

## Security Best Practices

**📖 See [SECURITY.md](SECURITY.md) for comprehensive security documentation.**

Key security practices:

1. **Never commit secrets**:
   - User secrets are stored outside the project directory
   - Environment variables are set at runtime
   - appsettings.json should NOT contain passwords

2. **Use encrypted connections**:
   - SQL connection encryption is enforced by default
   - SharePoint uses HTTPS

3. **Rotate credentials regularly**:
   ```bash
   dotnet user-secrets set "SharePoint:Password" "new-password"
   ```

### Credential Setup

```bash
# Windows: store credentials in the current user's DPAPI-protected store
dotnet run --project src/SPO2SQL.Console/SPO2SQL.Console.csproj -- --setup-secrets
```

Configuration is validated at startup by the host, so an invalid connection
string or an incomplete authentication configuration fails fast with a clear message.

## Troubleshooting

### Configuration Validation Errors

If you see validation errors on startup:
```
Configuration validation failed:
  - SharePoint username is required
```

**Solution**: Ensure secrets are set correctly:
```bash
dotnet user-secrets list
```

### User Secrets Not Found

**Symptoms**: Application uses empty strings for credentials

**Solution**: Verify UserSecretsId in .csproj matches your secrets storage:
```bash
dotnet user-secrets list
```

### Environment Variables Not Working

**Symptoms**: Credentials not loaded from environment

**Solution**: Ensure variables use the `SPO2SQL_` prefix and double underscores:
```bash
# Correct:
export SPO2SQL_SharePoint__Username="user@company.com"

# Incorrect:
export SharePoint:Username="user@company.com"  # Missing prefix
export SPO2SQL_SharePoint:Username="user@company.com"  # Wrong separator
```

## Next Steps

The modernization is ongoing. Current status:

✅ **Completed**:
- Dependency injection infrastructure
- Configuration system with User Secrets
- Structured logging setup
- Modern C# project structure

🚧 **In Progress**:
- Converting services to async/await
- Implementing service interfaces
- Adding Polly for resilience
- Creating unit and integration tests

📋 **Planned**:
- Docker support
- Health check endpoints
- Performance optimizations
- Enhanced observability

## Additional Documentation

- **[SECURITY.md](SECURITY.md)**: Comprehensive security guide
  - Credential management best practices
  - Network security requirements
  - Audit logging recommendations
  - Credential rotation procedures
  - Production deployment checklist
  - Security incident response

- **[PROGRESS.md](PROGRESS.md)**: Modernization progress tracker

## Getting Help

For issues or questions:
1. Check this README
2. Review [SECURITY.md](SECURITY.md) for security-related topics
3. Run `./scripts/verify-config.sh` to check your configuration
4. Review logs with `--verbose=Debug`
5. Check the main README.md for general documentation
6. Open an issue on the repository

