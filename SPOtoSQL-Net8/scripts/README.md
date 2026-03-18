# Helper Scripts

This directory contains utility scripts to help with secure configuration and deployment of the SharePoint to SQL Sync Tool.

## Scripts Overview

### setup-secrets.sh (Linux/Mac)

Interactive script to configure User Secrets for development.

**Usage**:
```bash
./scripts/setup-secrets.sh
```

**Features**:
- ✅ Validates .NET SDK installation
- ✅ Initializes User Secrets automatically
- ✅ Validates email format for SharePoint username
- ✅ Validates SharePoint URL format
- ✅ Validates SQL connection string format
- ✅ Checks for security best practices (encryption, password length)
- ✅ Masks passwords in output
- ✅ Provides guidance for next steps

**When to use**:
- First-time setup on a development machine
- Updating credentials
- Migrating from XML configuration

### setup-secrets.ps1 (Windows PowerShell)

Windows PowerShell version of the setup script with identical functionality.

**Usage**:
```powershell
.\scripts\setup-secrets.ps1
```

**Features**:
- Same as setup-secrets.sh but optimized for Windows
- Uses `Read-Host -AsSecureString` for password input
- PowerShell-native error handling

### verify-config.sh

Verifies that all required configuration is present and valid.

**Usage**:
```bash
# Verify User Secrets (development)
./scripts/verify-config.sh

# Verify Environment Variables (production)
./scripts/verify-config.sh --env-vars
```

**Checks performed**:
- ✅ All required configuration keys are present
- ✅ Email format validation
- ✅ URL format validation
- ✅ Connection string format validation
- ✅ Security checks (encryption, TrustServerCertificate)
- ✅ Password length validation
- ✅ Scans appsettings.json for accidentally committed secrets

**Exit codes**:
- `0` - Configuration is valid
- `1` - Configuration has errors

**When to use**:
- Before running the application
- After changing configuration
- In CI/CD pipelines to validate deployment
- Troubleshooting configuration issues

## Quick Start

### First-Time Development Setup

1. **Run the setup wizard**:
   ```bash
   cd SPOtoSQL-Net8
   ./scripts/setup-secrets.sh
   ```

2. **Follow the prompts** to enter:
   - SharePoint username (email)
   - SharePoint password
   - SharePoint site URL
   - SQL Server connection details

3. **Verify configuration**:
   ```bash
   ./scripts/verify-config.sh
   ```

4. **Run the application**:
   ```bash
   cd ConsoleApp1Net8
   dotnet run
   ```

### Production Deployment

1. **Set environment variables** (see SECURITY.md for details):
   ```bash
   export SPO2SQL_SharePoint__Username="user@company.com"
   export SPO2SQL_SharePoint__Password="your-password"
   export SPO2SQL_SharePoint__SiteUrl="https://company.sharepoint.com/sites/prod"
   export SPO2SQL_Sql__ConnectionString="Server=...;Database=...;Encrypt=true;..."
   ```

2. **Verify configuration**:
   ```bash
   ./scripts/verify-config.sh --env-vars
   ```

3. **Deploy and run**:
   ```bash
   dotnet publish -c Release
   cd bin/Release/net8.0/publish
   ./ConsoleApp1Net8
   ```

## Troubleshooting

### "dotnet: command not found"

**Problem**: .NET SDK is not installed or not in PATH.

**Solution**: Install .NET 8 SDK from https://dotnet.microsoft.com/download

### "Project file not found"

**Problem**: Scripts are being run from the wrong directory.

**Solution**: Always run scripts from the repository root:
```bash
cd /path/to/SPO2SQL
./SPOtoSQL-Net8/scripts/setup-secrets.sh
```

Or from the SPOtoSQL-Net8 directory:
```bash
cd /path/to/SPO2SQL/SPOtoSQL-Net8
./scripts/setup-secrets.sh
```

### "No secrets configured"

**Problem**: User Secrets have not been initialized or set.

**Solution**: Run the setup script:
```bash
./scripts/setup-secrets.sh
```

### Validation warnings

**Warning**: "Connection string should contain 'Encrypt=true'"

**Recommendation**: Always use encrypted connections in production. Update your connection string:
```bash
dotnet user-secrets set "Sql:ConnectionString" "Server=myServer;Database=myDB;User Id=user;Password=pwd;Encrypt=true;TrustServerCertificate=false;"
```

**Warning**: "Password is less than 8 characters"

**Recommendation**: Use strong passwords (12+ characters, mixed case, numbers, symbols).

## Security Notes

### What Gets Stored Where

**User Secrets** (Development):
- Location: `~/.microsoft/usersecrets/<UserSecretsId>/secrets.json`
- Format: JSON file with key-value pairs
- Permissions: User-readable only
- Automatically excluded from source control

**Environment Variables** (Production):
- Location: System or user environment variables
- Prefix: `SPO2SQL_`
- Separator: Double underscore `__` for nested sections
- Set via: Shell, systemd service file, Docker, Kubernetes, etc.

### Best Practices

1. **Never commit secrets to source control**
   - User Secrets are stored outside the project
   - appsettings.json should NOT contain passwords
   - The verify-config script checks for this

2. **Use encryption for all connections**
   - SQL: `Encrypt=true;TrustServerCertificate=false`
   - SharePoint: Always HTTPS

3. **Rotate credentials regularly**
   - Update secrets with setup script or manually
   - Test changes before deploying to production

4. **Use principle of least privilege**
   - SharePoint: Read-only access if possible
   - SQL: Minimal required permissions

5. **Audit and monitor**
   - Enable application logging
   - Review logs for authentication failures
   - Monitor for unusual activity

## Advanced Usage

### Automated Setup (Non-Interactive)

For CI/CD pipelines or automated deployments:

```bash
# Set secrets via dotnet CLI
cd ConsoleApp1Net8
dotnet user-secrets set "SharePoint:Username" "$SP_USERNAME"
dotnet user-secrets set "SharePoint:Password" "$SP_PASSWORD"
dotnet user-secrets set "SharePoint:SiteUrl" "$SP_SITEURL"
dotnet user-secrets set "Sql:ConnectionString" "$SQL_CONNSTR"

# Verify
cd ..
./scripts/verify-config.sh
if [ $? -ne 0 ]; then
    echo "Configuration validation failed"
    exit 1
fi
```

### Integration with CI/CD

Example GitHub Actions workflow:

```yaml
- name: Configure Secrets
  run: |
    cd SPOtoSQL-Net8/ConsoleApp1Net8
    dotnet user-secrets set "SharePoint:Username" "${{ secrets.SP_USERNAME }}"
    dotnet user-secrets set "SharePoint:Password" "${{ secrets.SP_PASSWORD }}"
    dotnet user-secrets set "SharePoint:SiteUrl" "${{ secrets.SP_SITEURL }}"
    dotnet user-secrets set "Sql:ConnectionString" "${{ secrets.SQL_CONNSTR }}"

- name: Verify Configuration
  run: |
    cd SPOtoSQL-Net8
    ./scripts/verify-config.sh
```

## See Also

- **[SECURITY.md](../SECURITY.md)**: Comprehensive security documentation
- **[README-MODERNIZATION.md](../README-MODERNIZATION.md)**: Configuration and deployment guide
- **[.NET User Secrets Documentation](https://docs.microsoft.com/en-us/aspnet/core/security/app-secrets)**: Official Microsoft documentation
