# SPO2SQL

[![Build and test](https://github.com/kill74/SPO2SQL/actions/workflows/ci.yml/badge.svg)](https://github.com/kill74/SPO2SQL/actions/workflows/ci.yml)
[![CodeQL](https://github.com/kill74/SPO2SQL/actions/workflows/codeql.yml/badge.svg)](https://github.com/kill74/SPO2SQL/actions/workflows/codeql.yml)
[![.NET 8](https://img.shields.io/badge/.NET-8-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)

**SPO2SQL** is a .NET 8 synchronization service that transfers SharePoint Online list data into SQL Server tables and applies data-quality routines during the process. It is designed for repeatable scheduled execution, explicit configuration, resilient network operations, and safe schema evolution.

## What it provides

- SharePoint Online access through CSOM and Microsoft identity authentication.
- Full and incremental synchronization modes.
- SQL table creation and additive column discovery.
- Per-list column mapping, destination names, data types, and ignore rules.
- Retry handling for transient SharePoint and network failures.
- Startup health checks for SharePoint and SQL connectivity.
- Operation statistics, structured logging, and cancellation support.
- Credential providers for environment variables, Windows DPAPI, and application configuration.
- Automated build, test, formatting, dependency-audit, and CodeQL workflows.

## Repository layout

```text
src/
├── SPO2SQL.Core/       SharePoint, SQL Server, security, configuration, and DQ logic
└── SPO2SQL.Console/    .NET host, options binding, startup, and orchestration

tests/
├── SPO2SQL.Core.Tests/     Core unit tests
└── SPO2SQL.Console.Tests/  Configuration and host-facing tests

docs/                   Configuration, security, modernization, and operational notes
```

### Core components

| Component | Responsibility |
| --- | --- |
| `SPO2SQL.Console.Application` | Starts the hosted application and coordinates health checks and synchronization |
| `RefreshSQLLists` | Orchestrates SharePoint-to-SQL transfers for configured lists |
| `SPOList` / `SPOUser` | Manages SharePoint list access and authentication |
| `SQLInteraction` | Creates or updates destination tables and transfers list items |
| `RetryPolicy` | Retries transient failures with exponential backoff and jitter |
| `HealthChecker` | Validates SharePoint and SQL connectivity |
| `CamlQueryBuilder` | Generates escaped CAML queries |
| `CredentialManager` | Resolves credentials through ordered providers |
| `ActivitiesDQ`, `InvoiceRequestDQ`, `TimesheetDQ` | List-specific data-quality processing |

## Requirements

- .NET SDK 8.0 or later
- Network access to SharePoint Online over HTTPS
- SQL Server access with permission to read source metadata and create/update destination tables
- A SharePoint account or supported application identity configured for the target tenant

The current authentication path uses username/password token acquisition. Environments requiring interactive MFA or Conditional Access should use an app-only/service-principal authentication design instead of relying on password-based authentication.

## Build and test

```bash
dotnet restore SPO2SQL.sln
dotnet build SPO2SQL.sln --no-restore -warnaserror
dotnet test SPO2SQL.sln --no-build --no-restore
```

For CI-equivalent formatting validation:

```bash
dotnet format SPO2SQL.sln --verify-no-changes --no-restore
```

Run the console host with:

```bash
dotnet run --project src/SPO2SQL.Console/SPO2SQL.Console.csproj
```

The synchronization mode is controlled by `Application:DailyMode`. Set it to `true` for incremental synchronization or `false` for a current-time/full transfer.

## Configuration

The console application binds these sections:

```json
{
  "Application": {
    "Name": "SPO2SQL",
    "Version": "2.0.0",
    "LogLevel": "Information",
    "EnableHealthChecks": true,
    "EnableMetrics": true,
    "DailyMode": true,
    "LegacyConfigPath": "XmlConfig/UserConfig.xml"
  },
  "SharePoint": {
    "SiteUrl": "https://contoso.sharepoint.com/sites/example",
    "Username": "user@contoso.com",
    "Password": "",
    "TimeoutSeconds": 120,
    "MaxRetries": 3,
    "InitialRetryDelayMs": 1000
  },
  "Sql": {
    "ConnectionString": "Server=.;Database=SPO2SQL;Integrated Security=true;Encrypt=true;TrustServerCertificate=false;",
    "CommandTimeoutSeconds": 300,
    "BatchSize": 80,
    "EnforceEncryption": true
  }
}
```

Do not commit passwords or connection strings. Use user secrets for development and environment variables or a managed secret store in production.

### Configuration precedence

The host loads configuration in this order, with later sources taking precedence:

1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. .NET user secrets
4. Environment variables
5. Environment variables prefixed with `SPO2SQL_`
6. Command-line configuration

Environment variables use double underscores for nested keys:

```bash
export SPO2SQL_SharePoint__Username="user@contoso.com"
export SPO2SQL_SharePoint__Password="use-a-secret-store"
export SPO2SQL_SharePoint__SiteUrl="https://contoso.sharepoint.com/sites/example"
export SPO2SQL_Sql__ConnectionString="Server=sql01;Database=SPO2SQL;Integrated Security=true;Encrypt=true;TrustServerCertificate=false;"
```

The bound options are bridged into the legacy synchronization layer at startup. `LegacyConfigPath` remains available for list mappings and older deployments; a missing legacy XML file is reported as a warning rather than a build-time failure.

## SharePoint list mappings

Legacy XML configuration supports per-list and per-column rules, including:

- SharePoint site/list selection
- SQL destination table names
- Column inclusion and exclusion
- Destination column names
- Explicit SQL data types
- List-level ignore rules

Keep deployment-specific XML outside source control when it contains credentials or internal infrastructure details. See [`docs/Configuration/README.md`](docs/Configuration/README.md) and [`docs/Configuration/MIGRATION_GUIDE.md`](docs/Configuration/MIGRATION_GUIDE.md).

## Security and operations

- Use least-privilege SharePoint and SQL identities.
- Require encrypted SQL connections in production.
- Never log passwords, access tokens, or complete connection strings.
- Prefer environment-backed or managed secret storage over files.
- Rotate credentials without modifying application binaries.
- Monitor authentication failures, retry exhaustion, schema changes, and synchronization statistics.
- Use cancellation-aware shutdowns for scheduled jobs and deployment rollouts.

The detailed security guidance is available in [`docs/SECURITY.md`](docs/SECURITY.md).

## CI/CD

GitHub Actions runs on pushes and pull requests:

1. Restore NuGet dependencies.
2. Build with warnings treated as errors.
3. Verify formatting.
4. Run unit tests with coverage collection.
5. Run the NuGet audit.

CodeQL analysis is configured separately under [`.github/workflows/codeql.yml`](.github/workflows/codeql.yml).

## Publishing

Example Windows deployment:

```bash
dotnet publish src/SPO2SQL.Console/SPO2SQL.Console.csproj \
  --configuration Release \
  --runtime win-x64 \
  --self-contained false
```

Published output is written below:

```text
src/SPO2SQL.Console/bin/Release/net8.0/win-x64/publish/
```

## License

This project is licensed under the MIT License.
