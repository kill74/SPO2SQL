# SharePoint Sync Tool

![.NET](https://img.shields.io/badge/.NET-8.0-blue)
![SharePoint CSOM](https://img.shields.io/badge/SharePoint%20CSOM-16.1-green)
![License](https://img.shields.io/badge/License-MIT-yellow.svg)

A console application to synchronise SharePoint Online lists with a SQL Server database, including data quality routines for common list inconsistencies. Built with the SharePoint Client Side Object Model (CSOM) and .NET 8.0.

---

## Table of Contents

- [Overview](#overview)
- [Features](#features)
- [Prerequisites](#prerequisites)
- [Installation](#installation)
- [Configuration](#configuration)
- [Usage](#usage)
- [Project Structure](#project-structure)
- [Troubleshooting](#troubleshooting)
- [License](#license)

---

## Overview

This tool automates the transfer of data from SharePoint Online lists to SQL Server tables and performs targeted data quality fixes on specific lists (e.g. Activities, Invoice Request). It supports two sync modes — **daily** and **monthly** — and provides detailed logging for monitoring and debugging.

---

## Features

**SharePoint → SQL Synchronisation**
Transfers data from configured SharePoint lists to corresponding SQL Server tables using either incremental (daily) or full (monthly) updates.

**Data Quality Fixes**

- _Activities_ — Backfills the `_OpportunityID` field by copying from `OpportunityID` where null.
- _Invoice Request_ — Updates approver fields (`Main_x0020_approver`, `Optional_x0020_approver`, `Financial_x0020_approver`) based on recent changes in the Unit list.
- _Timesheet_ — Similar data quality logic, extendable as needed.

**SQL Connection Testing**
Verifies connectivity to SQL Server and checks for necessary permissions (SELECT, CREATE TABLE, etc.) before any operation begins.

**Observability & Metrics**

- _Operation Statistics_ — Tracks execution metrics (total items processed, success/failure counts, duration, success rate, average time per item).
- _Operation Context_ — Correlates operations with unique IDs for end-to-end log tracing and debugging.

**Resilience & Error Handling**

- _Retry Policy_ — Automatically retries transient SharePoint failures (throttling, timeouts) with exponential backoff and jitter.
- _Health Checks_ — Validates SharePoint and SQL configurations at startup, preventing silent failures.

**Developer Experience**

- _CAML Query Builder_ — Type-safe helpers for constructing SharePoint queries (null checks, date ranges, text searches, complex conditions) without manual XML construction.

**Configurable Logging**
Verbosity levels (0–3) control console output — run silently in production or in full debug mode during development.

**External Configuration**
All settings (SharePoint credentials, SQL connection string) are stored in an XML file, overridable via a command-line argument.

---

## Prerequisites

- Windows operating system (.NET 8.0 target)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- Access to a SharePoint Online tenant with appropriate read/write permissions
- SQL Server (any edition) with a database where target tables will be created/updated
- VPN access if SharePoint or SQL Server is only reachable within a corporate network

---

## Installation

**1. Clone the repository**

```bash
git clone https://github.com/your-org/sharepoint-sync-tool.git
```

**2. Restore NuGet packages and build**

```bash
cd SPOtoSQL-Net8
dotnet restore
dotnet build ConsoleApp1Net8
```

**3. (Optional) Run the application**

```bash
dotnet run --project ConsoleApp1Net8 -- [arguments]
```

**4. (Optional) Deploy**
Publish the application for deployment:

```bash
dotnet publish ConsoleApp1Net8 -c Release -r win-x64 --self-contained false
```

The published output will be in `ConsoleApp1Net8/bin/Release/net8.0/win-x64/publish`.

---

## Configuration

All runtime settings are defined in an XML file. The default location is `XmlConfig\UserConfig.xml` (relative to the executable). You can specify an alternative path with the `--config` argument.

**Example `UserConfig.xml`**

```xml
<?xml version="1.0" encoding="utf-8"?>
<Configuration>
  <SharePoint>
    <Username>user@company.com</Username>
    <Password>YourPassword</Password>
  </SharePoint>
  <SQL>
    <ConnectionString>Server=myServer;Database=myDB;Integrated Security=true;</ConnectionString>
  </SQL>
</Configuration>
```

> **Security Note:** Storing passwords in plain text is not recommended for production. Consider using encrypted configuration sections or environment variables.

| Element                | Description                                                   |
| ---------------------- | ------------------------------------------------------------- |
| `SharePoint/Username`  | The SharePoint Online user account (email format)             |
| `SharePoint/Password`  | The password for that account                                 |
| `SQL/ConnectionString` | A valid ADO.NET connection string to your SQL Server database |

> **Note:** The tool expects specific SharePoint site relative paths (e.g. `seed`, `wolf`, `selfservice/invoicerequest`). These are hard-coded in the data quality classes. If your site structure differs, adjust the source code accordingly.

---

## Usage

Run the executable from the command line:

```bash
ConsoleApp1Net8.exe [arguments]
```

Or via `dotnet run`:

```bash
dotnet run --project ConsoleApp1Net8 -- [arguments]
```

### Arguments

| Argument          | Description                                                             |
| ----------------- | ----------------------------------------------------------------------- |
| `daily`           | Perform incremental sync (new/changed items only)                       |
| `monthly`         | Perform a full sync (all items)                                         |
| `diagnostic`      | Enable diagnostic mode (sets verbosity to 1 if not otherwise specified) |
| `--verbose=<0-3>` | Set verbosity: `0` = quiet, `1` = normal, `2` = detailed, `3` = debug   |
| `--config=<path>` | Use an alternative configuration file                                   |

### Examples

```bash
# Daily sync with normal logging
ConsoleApp1Net8.exe daily

# Monthly sync with detailed logging and a custom config
ConsoleApp1Net8.exe monthly --verbose=2 --config="C:\Configs\custom.xml"

# Test SQL connection and exit
ConsoleApp1Net8.exe diagnostic
```

### What happens during execution?

1. Command-line arguments are parsed and logging is configured.
2. The SQL Server connection is tested (including permission checks).
3. SharePoint credentials are loaded from the configuration file.
4. **[NEW]** Health checks validate SharePoint and SQL configuration.
5. Based on the mode (`daily` or `monthly`), `RefreshSQLLists.SPOtoSQLUpdate(...)` performs the data transfer.
6. Data quality routines (`ActivitiesDQ`, `InvoiceRequestDQ`, etc.) run as part of the sync.
7. All actions are logged to the console at the specified verbosity level.
8. **[NEW]** Operation metrics are tracked and reported via `OperationStatistics`.

---

## Utility Classes Guide

### OperationStatistics

Tracks execution metrics for any batch operation:

```csharp
var stats = new OperationStatistics();
// ... perform operations ...
stats.TotalItemsProcessed = 100;
stats.SuccessfulUpdates = 95;
stats.FailedUpdates = 5;
Console.WriteLine(stats); // Outputs: "Processed 100 items; 95 succeeded, 5 failed (95% success rate, avg 250ms/item)"
```

### OperationContext

Maintains correlation ID and statistics for entire operations:

```csharp
var context = new OperationContext { OperationName = "DailySync" };
// ... perform operations, update context.Statistics ...
context.MarkComplete(); // Logs completion with correlation ID
```

### HealthChecker

Validates configuration at startup:

```csharp
var checker = new HealthChecker(Logger.Instance, verbosity: 2);
var result = checker.PerformHealthCheck(spoUser, sqlConnectionString);
if (!result.IsHealthy)
{
    Console.WriteLine(result); // Prints errors and warnings
    return; // Exit if critical issues found
}
```

### RetryPolicy

Automatically retries transient failures:

```csharp
var policy = new RetryPolicy(maxRetries: 3, initialDelayMs: 1000);
var result = policy.ExecuteWithRetry(
    () => performSharePointOperation(),
    "PerformSharePointOperation"
);
```

### CamlQueryBuilder

Construct type-safe CAML queries:

```csharp
// Query for null fields
var query1 = CamlQueryBuilder.BuildNullFieldQuery("ApprovalStatus");

// Query for date range
var query2 = CamlQueryBuilder.BuildDateRangeQuery("Created",
    DateTime.Now.AddDays(-7), DateTime.Now);

// Complex query with AND conditions
var query3 = CamlQueryBuilder.BuildAndQuery(
    CamlQueryBuilder.BuildNotNullFieldQuery("Owner"),
    CamlQueryBuilder.BuildEqualTextQuery("Status", "Active")
);
```

---

## Project Structure

```
SPOtoSQL-Net8/
├── ConsoleApp1Net8/
│   ├── ConsoleApp1Net8.csproj
│   └── ... (source files linked from original location)
SPOtoSQL-Snapshots/
└── ConsoleApp1/
    ├── ConsoleApp1.csproj          (original .NET Framework 4.8 project)
    ├── packages.config
    ├── AssemblyInfo.cs
    ├── ConsoleLogger/
    │   └── Logger.cs
    ├── Sharepoint/
    │   ├── ActivitiesDQ.cs
    │   ├── CamlQueryBuilder.cs      (CAML query builder)
    │   ├── Context.cs
    │   ├── GetallLists.cs
    │   ├── HealthChecker.cs         (health check validation)
    │   ├── InvoiceRequestDQ.cs
    │   ├── OperationContext.cs      (operation tracking with correlation ID)
    │   ├── OperationStatistics.cs   (execution metrics collection)
    │   ├── RetryPolicy.cs           (retry with exponential backoff)
    │   ├── SPOList.cs
    │   ├── SPOUser.cs
    │   └── TimesheetDQ.cs
    ├── SPODataQuality/
    │   └── RefreshSPOLists.cs
    ├── Sqlserver/
    │   ├── RefreshSQLLists.cs
    │   └── SQLInteraction.cs
    └── XmlConfig/
        ├── ConfigHelper.cs
        └── UserConfig.xml
```

| Component                           | Description                                                                          |
| ----------------------------------- | ------------------------------------------------------------------------------------ |
| `RefreshSPOLists`                   | Main entry point — argument handling, SQL test, and orchestration                    |
| `SPOList` / `SPOUser`               | Wrappers for SharePoint CSOM operations                                              |
| `ActivitiesDQ` / `InvoiceRequestDQ` | Data quality fixes for specific lists                                                |
| `RefreshSQLLists`                   | Handles the SharePoint → SQL data transfer                                           |
| `ConfigHelper`                      | Reads the XML configuration file                                                     |
| `Logger`                            | Console logger with configurable verbosity levels                                    |
| `OperationStatistics`               | Metrics collection (items processed, success/failure counts, duration, success rate) |
| `OperationContext`                  | Operation tracking with correlation ID for end-to-end log tracing                    |
| `RetryPolicy`                       | Automatic retry with exponential backoff for transient failures                      |
| `HealthChecker`                     | Pre-flight validation of SharePoint and SQL configuration                            |
| `CamlQueryBuilder`                  | Type-safe CAML query construction helpers                                            |

---

## Troubleshooting

| Problem                                         | Possible Solution                                                                                                                                                                                                                       |
| ----------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Cannot connect to SQL Server                    | Check VPN, firewall rules, and the connection string. Run `diagnostic` mode for more details. Enable health checks via `HealthChecker.PerformHealthCheck()` to validate configuration before operations.                                |
| SharePoint login fails                          | Verify username/password in the config file. Ensure the account has access to the specified site. Use `HealthChecker` to pre-validate credentials and connectivity.                                                                     |
| "Field not found" errors                        | Data quality classes expect specific field names (e.g. `OpportunityID`). If your lists differ, modify the source.                                                                                                                       |
| Throttling / slow performance                   | The tool batches updates (e.g. 80 items per batch) to avoid SharePoint limits. Adjust batch sizes if needed. Use `RetryPolicy` for automatic retry with exponential backoff on transient failures.                                      |
| Missing tables in SQL                           | The tool assumes tables already exist with the correct schema. Review `SQLInteraction.cs` for table creation logic.                                                                                                                     |
| Operation failures without clear error messages | Enable correlation tracking via `OperationContext` to trace operations through logs. Run with `--verbose=3` for full debug output. Check `OperationStatistics` for detailed execution metrics (items processed, success/failure rates). |
| Need to debug specific operations               | Use `OperationContext.CorrelationId` to correlate related log entries. Review operation metrics via `OperationStatistics.ToString()` for duration, success rate, and performance insights.                                              |

If you encounter unexpected behaviour, run with `--verbose=3` for full debug output and open an issue with the log attached.

---

## License

This project is licensed under the [MIT License](https://opensource.org/license/mit).
