# Security Guide - SharePoint to SQL Sync Tool

## Table of Contents
- [Overview](#overview)
- [Credential Management](#credential-management)
- [Network Security](#network-security)
- [Audit Logging](#audit-logging)
- [Credential Rotation](#credential-rotation)
- [Production Deployment Checklist](#production-deployment-checklist)
- [Security Incident Response](#security-incident-response)
- [Compliance](#compliance)

## Overview

This document outlines security best practices for deploying and operating the SharePoint to SQL Sync Tool. Security is implemented through multiple layers:

- **Credential Protection**: User Secrets (dev), Environment Variables (prod), never in source code
- **Transport Security**: Enforced TLS/SSL for all connections
- **Access Control**: Principle of least privilege for service accounts
- **Audit Logging**: Comprehensive logging of all operations
- **Configuration Validation**: Runtime validation of security settings

## Credential Management

### Development Environment

**Use .NET User Secrets** for local development:

```bash
# Navigate to project directory
cd SPOtoSQL-Net8/ConsoleApp1Net8

# Set credentials (stored encrypted outside project directory)
dotnet user-secrets set "SharePoint:Username" "dev@company.com"
dotnet user-secrets set "SharePoint:Password" "your-dev-password"
dotnet user-secrets set "Sql:ConnectionString" "Server=dev-sql;Database=DevDB;Integrated Security=true;"
```

**Storage Location**:
- **Windows**: `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json`
- **Linux/Mac**: `~/.microsoft/usersecrets/<UserSecretsId>/secrets.json`

**Security Benefits**:
- ✅ Secrets stored outside project directory
- ✅ Not committed to source control
- ✅ Isolated per-user configuration
- ✅ Easy to update and rotate

### Production Environment

**Use Environment Variables** with the `SPO2SQL_` prefix:

```bash
# Linux/Mac
export SPO2SQL_SharePoint__Username="prod@company.com"
export SPO2SQL_SharePoint__Password="your-secure-password"
export SPO2SQL_SharePoint__SiteUrl="https://company.sharepoint.com/sites/prod"
export SPO2SQL_Sql__ConnectionString="Server=prod-sql;Database=ProdDB;Encrypt=true;TrustServerCertificate=false;User Id=spo2sql_service;Password=db-password"
```

```powershell
# Windows (PowerShell)
$env:SPO2SQL_SharePoint__Username="prod@company.com"
$env:SPO2SQL_SharePoint__Password="your-secure-password"
$env:SPO2SQL_SharePoint__SiteUrl="https://company.sharepoint.com/sites/prod"
$env:SPO2SQL_Sql__ConnectionString="Server=prod-sql;Database=ProdDB;Encrypt=true;TrustServerCertificate=false;User Id=spo2sql_service;Password=db-password"
```

**For Windows Services or Scheduled Tasks**:
- Set as **System** or **User** environment variables via Control Panel
- Or configure in Task Scheduler environment settings
- Or use Azure Key Vault / AWS Secrets Manager (recommended for cloud deployments)

### Never Store Credentials In

❌ **appsettings.json** - This file is committed to source control  
❌ **Code files** - Hardcoded credentials are a critical vulnerability  
❌ **Build artifacts** - Published applications should not contain secrets  
❌ **Log files** - Ensure logging doesn't leak credentials  
❌ **Error messages** - Redact sensitive information from exceptions  

### Service Account Best Practices

**SharePoint Account**:
- Create dedicated service account (e.g., `spo2sql-service@company.com`)
- Grant **minimum required permissions** (Read-only if possible)
- Use a strong password (20+ characters, complex)
- Enable MFA if your SharePoint configuration supports service accounts with MFA
- Consider using Azure AD App Registration with Certificate authentication instead

**SQL Server Account**:
- Create dedicated SQL login/user (e.g., `spo2sql_service`)
- Grant only required permissions:
  - `db_datareader` - if only reading
  - `db_datawriter` - if inserting/updating
  - Specific table permissions instead of database roles
- Use SQL Server authentication with strong password
- Consider Windows Integrated Security if running on domain-joined server

### Connection String Security

**SQL Connection String Requirements**:

```
Server=myserver.database.windows.net;
Database=MyDatabase;
User Id=spo2sql_service;
Password=StrongP@ssw0rd123!;
Encrypt=true;                      /* REQUIRED - Enforces TLS encryption */
TrustServerCertificate=false;     /* REQUIRED - Validates server certificate */
Connection Timeout=30;
MultipleActiveResultSets=false;   /* Prevent connection pooling issues */
```

**Key Security Parameters**:
- `Encrypt=true` - **MANDATORY** - Encrypts all data in transit
- `TrustServerCertificate=false` - Validates SQL Server's SSL certificate
- Never use `TrustServerCertificate=true` in production (vulnerable to MITM attacks)

The application enforces `EnforceEncryption=true` in appsettings.json to validate encryption is enabled.

## Network Security

### Firewall Configuration

**Outbound Rules (from application server)**:
- SharePoint Online: HTTPS (443) to `*.sharepoint.com`
- SQL Server: TDS (1433 or custom port) to database server
- DNS: UDP/TCP (53) for name resolution

**Inbound Rules**:
- No inbound connections required (application initiates all connections)

### Network Segmentation

**Recommended Architecture**:
```
[Application Server]  
    ↓ HTTPS (TLS 1.2+)
[SharePoint Online]

[Application Server]  
    ↓ TDS (Encrypted)
[SQL Server]
```

**Best Practices**:
- Deploy application server in DMZ or isolated VLAN
- Use private endpoints for Azure SQL Database if in Azure
- Implement network security groups/ACLs to restrict traffic
- Monitor network traffic for anomalies

### TLS/SSL Requirements

**Minimum Versions**:
- SharePoint: TLS 1.2+ (SharePoint Online requirement)
- SQL Server: TLS 1.2+ (configured via `Encrypt=true`)
- Disable SSL 3.0, TLS 1.0, TLS 1.1 (deprecated protocols)

## Audit Logging

### What Gets Logged

The application logs the following security-relevant events:

**Authentication Events**:
- SharePoint authentication success/failure
- SQL Server connection success/failure
- Configuration validation errors

**Data Access Events**:
- SharePoint list access (list name, item count)
- SQL operations (table, operation type, row count)
- Batch processing statistics

**Security Events**:
- Configuration validation failures
- Credential loading errors
- Network connection failures
- Retry attempts and circuit breaker activations

### Log Configuration

**appsettings.json**:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "System": "Warning"
    },
    "Console": {
      "FormatterName": "simple",
      "FormatterOptions": {
        "TimestampFormat": "yyyy-MM-dd HH:mm:ss ",
        "UseUtcTimestamp": true  /* Use UTC for audit logs */
      }
    }
  }
}
```

**Production Recommendations**:
- Set `LogLevel.Default` to `"Information"` or higher
- Use `"UseUtcTimestamp": true` for consistent audit trail
- Configure log retention (30-90 days minimum)
- Forward logs to SIEM system (Splunk, Azure Monitor, CloudWatch)
- Alert on authentication failures, configuration errors, exceptions

### Sensitive Data Redaction

The application automatically redacts:
- Passwords in connection strings (logged as `***`)
- SharePoint credentials (never logged)
- SQL credentials (never logged)

**Example Log Output**:
```
2024-01-15 10:30:45 [Information] SharePoint authentication successful for user: s***@company.com
2024-01-15 10:30:46 [Information] SQL connection established to database: ProductionDB (encryption: True)
2024-01-15 10:30:50 [Information] Processed 1,234 items from SharePoint list 'Invoices'
```

### Audit Log Review

**Daily**:
- Review authentication failures
- Check for configuration validation errors
- Monitor exception rates

**Weekly**:
- Analyze data access patterns
- Review performance metrics
- Check for retry/circuit breaker activations

**Monthly**:
- Full security audit of logs
- Review user access (service account activity)
- Validate log retention and archival

## Credential Rotation

### Rotation Schedule

**Recommended Intervals**:
- SharePoint passwords: Every 90 days
- SQL Server passwords: Every 90 days
- Service account passwords: Every 60 days (critical systems)
- Emergency rotation: Immediately upon suspected compromise

### Rotation Procedure

#### SharePoint Password Rotation

1. **Change password in Microsoft 365**:
   - Log in to Microsoft 365 Admin Center
   - Navigate to Users > Active Users
   - Select service account (e.g., `spo2sql-service@company.com`)
   - Reset password

2. **Update application configuration**:

   **Development**:
   ```bash
   dotnet user-secrets set "SharePoint:Password" "new-password-here"
   ```

   **Production (Linux/Mac)**:
   ```bash
   # Update environment variable
   export SPO2SQL_SharePoint__Password="new-password-here"
   
   # Restart application
   systemctl restart spo2sql.service
   ```

   **Production (Windows)**:
   ```powershell
   # Update system environment variable
   [Environment]::SetEnvironmentVariable("SPO2SQL_SharePoint__Password", "new-password-here", "Machine")
   
   # Restart service
   Restart-Service -Name "SPO2SQLService"
   ```

3. **Verify**:
   ```bash
   # Check logs for successful authentication
   journalctl -u spo2sql.service -n 50
   ```

#### SQL Server Password Rotation

1. **Change password in SQL Server**:
   ```sql
   -- Connect as sysadmin
   ALTER LOGIN spo2sql_service WITH PASSWORD = 'new-strong-password';
   ```

2. **Update connection string**:

   **Development**:
   ```bash
   dotnet user-secrets set "Sql:ConnectionString" "Server=myserver;Database=MyDB;User Id=spo2sql_service;Password=new-strong-password;Encrypt=true;TrustServerCertificate=false;"
   ```

   **Production**:
   ```bash
   export SPO2SQL_Sql__ConnectionString="Server=myserver;Database=MyDB;User Id=spo2sql_service;Password=new-strong-password;Encrypt=true;TrustServerCertificate=false;"
   systemctl restart spo2sql.service
   ```

3. **Test connection**:
   ```bash
   # Run application with verbose logging
   dotnet run -- --verbose
   ```

### Emergency Credential Rotation

**If credentials are compromised**:

1. ⚠️ **Immediately** change passwords in SharePoint/SQL Server
2. Update application configuration
3. Restart application
4. Review audit logs for unauthorized access
5. Document incident
6. Review and update security procedures

## Production Deployment Checklist

### Pre-Deployment

- [ ] **Service Accounts Created**
  - SharePoint service account with minimum permissions
  - SQL service account with minimum permissions
  - Credentials documented in secure password manager

- [ ] **Network Security**
  - Firewall rules configured (outbound HTTPS, SQL)
  - Network segmentation in place
  - DNS resolution tested

- [ ] **Environment Variables**
  - All required variables set with `SPO2SQL_` prefix
  - Connection strings include `Encrypt=true;TrustServerCertificate=false`
  - Credentials match service accounts

- [ ] **Application Configuration**
  - appsettings.json reviewed (no secrets present)
  - Logging configured for production
  - Timeouts and batch sizes tuned

- [ ] **Security Hardening**
  - Server OS patched and updated
  - Antivirus/EDR installed and configured
  - Unnecessary services disabled
  - Application runs as non-admin user

### Deployment

- [ ] **Build and Publish**
  ```bash
  dotnet publish -c Release -r linux-x64 --self-contained false -o ./publish
  ```

- [ ] **File Permissions**
  ```bash
  # Application files
  chown -R spo2sql-user:spo2sql-group ./publish
  chmod 750 ./publish
  chmod 640 ./publish/appsettings*.json
  ```

- [ ] **Configuration Validation**
  ```bash
  # Run verify-config script
  ./scripts/verify-config.sh
  ```

- [ ] **Test Execution**
  ```bash
  # Dry run to validate configuration
  ./publish/ConsoleApp1Net8 --validate-only
  ```

### Post-Deployment

- [ ] **Logging Verification**
  - Logs writing to expected location
  - Log level appropriate for production
  - No sensitive data in logs

- [ ] **Monitoring Setup**
  - Application logs forwarded to SIEM
  - Performance metrics collected
  - Alerts configured for failures

- [ ] **Backup and Recovery**
  - Database backup schedule verified
  - Application configuration backed up
  - Recovery procedures documented

- [ ] **Documentation**
  - Deployment documented
  - Service account credentials stored securely
  - Runbook created for operations team

- [ ] **Security Validation**
  - Vulnerability scan performed
  - Penetration test scheduled
  - Security review completed

### Ongoing Operations

- [ ] **Daily**
  - Review logs for errors/warnings
  - Monitor application health
  - Verify data synchronization

- [ ] **Weekly**
  - Review performance metrics
  - Check disk space and logs rotation
  - Verify backups completed successfully

- [ ] **Monthly**
  - Security audit of logs
  - Review service account activity
  - Test credential rotation procedure

- [ ] **Quarterly**
  - Rotate service account passwords
  - Review and update security procedures
  - Security training for operations team

## Security Incident Response

### Suspected Credential Compromise

**Immediate Actions**:
1. Disable/change compromised credentials immediately
2. Review audit logs for unauthorized access
3. Identify scope of potential data exposure
4. Update all systems using compromised credentials

**Investigation**:
1. Review logs for 30 days prior to detection
2. Identify all access by compromised account
3. Check for data exfiltration or unauthorized changes
4. Document timeline and findings

**Recovery**:
1. Rotate all credentials (even if only one suspected)
2. Review and strengthen access controls
3. Update security procedures
4. Schedule follow-up security review

### Unauthorized Access Detected

**Immediate Actions**:
1. Isolate affected systems (network disconnect if necessary)
2. Preserve logs and evidence
3. Disable compromised accounts
4. Alert security team and management

**Investigation**:
1. Engage incident response team
2. Analyze logs for intrusion vector
3. Identify compromised data
4. Assess business impact

**Recovery**:
1. Remove unauthorized access
2. Patch vulnerabilities
3. Restore from clean backups if necessary
4. Implement additional monitoring
5. Conduct post-incident review

### Data Breach

**Immediate Actions**:
1. Contain the breach (isolate systems)
2. Preserve evidence
3. Alert management and legal team
4. Begin impact assessment

**Legal and Compliance**:
1. Determine breach notification requirements (GDPR, HIPAA, etc.)
2. Document incident details
3. Prepare notifications for affected parties
4. Coordinate with legal counsel

**Recovery**:
1. Eradicate attack vector
2. Strengthen security controls
3. Monitor for secondary attacks
4. Conduct lessons-learned review

## Compliance

### Data Protection Regulations

**GDPR** (EU General Data Protection Regulation):
- Ensure data minimization (only sync required data)
- Implement data retention policies
- Provide audit trail for data access
- Support data deletion requests

**HIPAA** (Health Insurance Portability and Accountability Act):
- Encrypt data in transit (enforced via `Encrypt=true`)
- Implement access controls (service accounts)
- Maintain audit logs (configured logging)
- Conduct regular security assessments

### Security Standards

**NIST Cybersecurity Framework**:
- Identify: Asset inventory, risk assessment
- Protect: Access controls, encryption, secure configuration
- Detect: Logging, monitoring, anomaly detection
- Respond: Incident response plan, communication
- Recover: Backup and restore procedures

**CIS Controls**:
- Control 3: Data Protection (encryption)
- Control 4: Secure Configuration (hardened settings)
- Control 6: Access Control Management (least privilege)
- Control 8: Audit Log Management (comprehensive logging)

### Audit and Attestation

**Prepare for Audits**:
- Maintain configuration documentation
- Preserve audit logs (minimum 90 days)
- Document security procedures
- Track security incidents and responses

**Evidence Collection**:
- Configuration files (redacted)
- Audit logs
- Access control lists
- Incident response documentation
- Security training records

## Additional Resources

### Helper Scripts

The `scripts/` directory contains tools to assist with secure configuration:

- **setup-secrets.sh** / **setup-secrets.ps1**: Interactive script to configure User Secrets
- **verify-config.sh**: Validates configuration without exposing credentials

### Documentation

- **README-MODERNIZATION.md**: Configuration and deployment guide
- **appsettings.json**: Application configuration reference
- **PROGRESS.md**: Modernization roadmap

### External Resources

- [.NET User Secrets Documentation](https://docs.microsoft.com/en-us/aspnet/core/security/app-secrets)
- [SQL Server Connection String Security](https://docs.microsoft.com/en-us/sql/connect/ado-net/connection-string-syntax)
- [SharePoint Online Security](https://docs.microsoft.com/en-us/sharepoint/security-for-sharepoint-server/security-for-sharepoint-server)
- [OWASP Application Security](https://owasp.org/www-project-application-security-verification-standard/)

## Contact

For security concerns or to report vulnerabilities:
- **Internal**: Contact your security team
- **External**: Follow responsible disclosure practices

---

**Document Version**: 1.0  
**Last Updated**: 2024-01-15  
**Review Schedule**: Quarterly
