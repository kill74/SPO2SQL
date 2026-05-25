# SPO2SQL

[![.NET](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/download)
[![CI](https://github.com/your-org/SPO2SQL/actions/workflows/ci.yml/badge.svg)](.github/workflows/ci.yml)
[![CodeQL](https://github.com/your-org/SPO2SQL/actions/workflows/codeql.yml/badge.svg)](.github/workflows/codeql.yml)

SharePoint Online → SQL Server sync with data quality routines. Uses CSOM + .NET 8.

## Quick start

```bash
dotnet restore SPO2SQL.sln
dotnet build SPO2SQL.sln
dotnet run --project src/SPO2SQL.Console -- daily
```

## Configuration

**appsettings.json** (new) — DI-hosted settings:

```json
{
  "Application": {
    "Name": "SPO2SQL",
    "DailyMode": true,
    "EnableHealthChecks": true,
    "EnableMetrics": true
  },
  "SharePoint": {
    "SiteUrl": "https://contoso.sharepoint.com",
    "Username": "user@contoso.com",
    "Password": "",
    "TimeoutSeconds": 300,
    "MaxRetries": 3
  },
  "Sql": {
    "ConnectionString": "Server=.;Database=SPO2SQL;Integrated Security=true;",
    "CommandTimeoutSeconds": 300,
    "BatchSize": 80
  }
}
```

**XmlConfig/UserConfig.xml** (legacy) — fallback for credentials and per-list mappings. Path overridable via `--config`.

### Runtime arguments

| Argument | Description |
|----------|-------------|
| `daily` | Incremental sync (new/changed items) |
| `monthly` | Full sync |
| `diagnostic` | Test connectivity and exit |
| `--verbose=<0-3>` | 0=quiet, 1=normal, 2=detailed, 3=debug |
| `--config=<path>` | Alternative legacy config path |

## Architecture

```
src/
├── SPO2SQL.Core/          Domain: CSOM wrappers, SQL transfer, DQ routines, logging
└── SPO2SQL.Console/       Host: DI, config binding, startup orchestration
tests/
├── SPO2SQL.Core.Tests/    Unit tests (xUnit, FluentAssertions)
└── SPO2SQL.Console.Tests/
.github/workflows/
├── ci.yml                 Build, test, format check, coverage, NuGet audit
└── codeql.yml             Security analysis (weekly + PRs)
.gitlab-ci.yml             Equivalent pipeline for GitLab
```

### Key components

| Component | Responsibility |
|-----------|---------------|
| `RefreshSQLLists` | Orchestrates SPO → SQL data transfer per list |
| `RefreshSPOLists` | Syncs field schemas between source/destination lists |
| `SPOList` / `SPOUser` | CSOM wrappers with connection management |
| `SQLInteraction` | SQL table create/alter, bulk insert, metadata tracking |
| `ActivitiesDQ`, `InvoiceRequestDQ`, `TimesheetDQ` | Per-list data quality fixes |
| `RetryPolicy` | Exponential backoff with jitter for transient failures |
| `HealthChecker` | Pre-flight validation of SPO + SQL config |
| `CamlQueryBuilder` | Type-safe CAML query construction |
| `OperationStatistics` | Execution metrics (success rate, duration, throughput) |
| `CredentialManager` | Composite credential source: DPAPI → env vars → XML fallback |

## Build guarantees

- `TreatWarningsAsErrors` + `AnalysisLevel=latest` — zero warnings policy
- `dotnet format --verify-no-changes` enforced in CI
- CodeQL security-and-quality queries run on every PR
- NuGet vulnerability audit fails the build on known CVEs

## Deploy

```bash
dotnet publish src/SPO2SQL.Console -c Release -r win-x64 --self-contained false
```

Output: `src/SPO2SQL.Console/bin/Release/net8.0/win-x64/publish/`

## License

MIT
