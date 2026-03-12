# SharePoint Sync Tool

![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.8-blue)
![SharePoint CSOM](https://img.shields.io/badge/SharePoint%20CSOM-16.1-green)
![License](https://img.shields.io/badge/License-MIT-yellow.svg)

A console application to synchronise SharePoint Online lists with a SQL Server database, including data quality routines for common list inconsistencies. Built with the SharePoint Client Side Object Model (CSOM) and .NET Framework 4.8.

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
- *Activities* — Backfills the `_OpportunityID` field by copying from `OpportunityID` where null.
- *Invoice Request* — Updates approver fields (`Main_x0020_approver`, `Optional_x0020_approver`, `Financial_x0020_approver`) based on recent changes in the Unit list.
- *Timesheet* — Similar data quality logic, extendable as needed.

**SQL Connection Testing**
Verifies connectivity to SQL Server and checks for necessary permissions (SELECT, CREATE TABLE, etc.) before any operation begins.

**Configurable Logging**
Verbosity levels (0–3) control console output — run silently in production or in full debug mode during development.

**External Configuration**
All settings (SharePoint credentials, SQL connection string) are stored in an XML file, overridable via a command-line argument.

---

## Prerequisites

- Windows operating system (.NET Framework 4.8 target)
- [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework/net48) or later
- Visual Studio 2019 or 2022 (for building from source)
- Access to a SharePoint Online tenant with appropriate read/write permissions
- SQL Server (any edition) with a database where target tables will be created/updated
- VPN access if SharePoint or SQL Server is only reachable within a corporate network

---

## Installation

**1. Clone the repository**
```bash
git clone https://github.com/your-org/sharepoint-sync-tool.git
```

**2. Open the solution in Visual Studio**
```
ConsoleApp1.sln
```

**3. Restore NuGet packages**

The main dependency is `Microsoft.SharePointOnline.CSOM` (v16.1). Visual Studio should restore it automatically. If not, run:
```
Update-Package -reinstall
```

**4. Build the solution**

Go to *Build → Build Solution*. The executable will be placed in `bin\Debug` or `bin\Release`.

**5. (Optional) Deploy**

Copy the `bin\Release` folder to any Windows machine with .NET Framework 4.8 installed.

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

> ⚠️ **Security Note:** Storing passwords in plain text is not recommended for production. Consider using encrypted configuration sections or environment variables.

| Element | Description |
|---|---|
| `SharePoint/Username` | The SharePoint Online user account (email format) |
| `SharePoint/Password` | The password for that account |
| `SQL/ConnectionString` | A valid ADO.NET connection string to your SQL Server database |

> **Note:** The tool expects specific SharePoint site relative paths (e.g. `seed`, `wolf`, `selfservice/invoicerequest`). These are hard-coded in the data quality classes. If your site structure differs, adjust the source code accordingly.

---

## Usage

Run the executable from the command line:

```
ConsoleApp1.exe [arguments]
```

### Arguments

| Argument | Description |
|---|---|
| `daily` | Perform incremental sync (new/changed items only) |
| `monthly` | Perform a full sync (all items) |
| `diagnostic` | Enable diagnostic mode (sets verbosity to 1 if not otherwise specified) |
| `--verbose=<0-3>` | Set verbosity: `0` = quiet, `1` = normal, `2` = detailed, `3` = debug |
| `--config=<path>` | Use an alternative configuration file |

### Examples

```bash
# Daily sync with normal logging
ConsoleApp1.exe daily

# Monthly sync with detailed logging and a custom config
ConsoleApp1.exe monthly --verbose=2 --config="C:\Configs\custom.xml"

# Test SQL connection and exit
ConsoleApp1.exe diagnostic
```

### What happens during execution?

1. Command-line arguments are parsed and logging is configured.
2. The SQL Server connection is tested (including permission checks).
3. SharePoint credentials are loaded from the configuration file.
4. Based on the mode (`daily` or `monthly`), `RefreshSQLLists.SPOtoSQLUpdate(...)` performs the data transfer.
5. Data quality routines (`ActivitiesDQ`, `InvoiceRequestDQ`, etc.) run as part of the sync.
6. All actions are logged to the console at the specified verbosity level.

---

## Project Structure

```
ConsoleApp1/
├── ConsoleApp1.csproj
├── packages.config
├── AssemblyInfo.cs
├── ConsoleLogger/
│   └── Logger.cs
├── Sharepoint/
│   ├── ActivitiesDQ.cs
│   ├── Context.cs
│   ├── GetallLists.cs
│   ├── InvoiceRequestDQ.cs
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

| Component | Description |
|---|---|
| `RefreshSPOLists` | Main entry point — argument handling, SQL test, and orchestration |
| `SPOList` / `SPOUser` | Wrappers for SharePoint CSOM operations |
| `ActivitiesDQ` / `InvoiceRequestDQ` | Data quality fixes for specific lists |
| `RefreshSQLLists` | Handles the SharePoint → SQL data transfer |
| `ConfigHelper` | Reads the XML configuration file |
| `Logger` | Console logger with configurable verbosity levels |

---

## Troubleshooting

| Problem | Possible Solution |
|---|---|
| Cannot connect to SQL Server | Check VPN, firewall rules, and the connection string. Run `diagnostic` mode for more details. |
| SharePoint login fails | Verify username/password in the config file. Ensure the account has access to the specified site. |
| "Field not found" errors | Data quality classes expect specific field names (e.g. `OpportunityID`). If your lists differ, modify the source. |
| Throttling / slow performance | The tool batches updates (e.g. 80 items per batch) to avoid SharePoint limits. Adjust batch sizes if needed. |
| Missing tables in SQL | The tool assumes tables already exist with the correct schema. Review `SQLInteraction.cs` for table creation logic. |

If you encounter unexpected behaviour, run with `--verbose=3` for full debug output and open an issue with the log attached.

---

## License

This project is licensed under the [MIT License](https://opensource.org/license/mit).
