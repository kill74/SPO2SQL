SharePoint Sync Tool
https://img.shields.io/badge/.NET%2520Framework-4.8-blue
https://img.shields.io/badge/SharePoint%2520CSOM-16.1-green
https://img.shields.io/badge/License-MIT-yellow.svg

A console application to synchronize SharePoint Online lists with a SQL Server database, including data quality routines for common list inconsistencies.
Built with the SharePoint Client Side Object Model (CSOM) and .NET Framework 4.8.

Table of Contents
Overview

Features

Prerequisites

Installation

Configuration

Usage

Project Structure

Troubleshooting

License

Português

Overview
This tool was developed to automate the transfer of data from SharePoint Online lists to SQL Server tables, as well as to perform targeted data quality fixes on specific lists (e.g., Activities, Invoice Request). It supports two sync modes (daily and monthly) and provides detailed logging for monitoring and debugging.

Features
SharePoint → SQL Synchronisation
Transfers data from configured SharePoint lists to corresponding SQL Server tables using either incremental (daily) or full (monthly) updates.

Data Quality Fixes

Activities: Backfills the _OpportunityID field by copying the value from OpportunityID where it is null.

Invoice Request: Updates approver fields (Main_x0020_approver, Optional_x0020_approver, Financial_x0020_approver) based on recent changes in the Unit list.

Timesheet: (similar data quality logic – extend as needed)

SQL Connection Testing
Before any operation, the tool verifies connectivity to the SQL Server and checks for necessary permissions (SELECT, CREATE TABLE, etc.).

Configurable Logging
Verbosity levels (0–3) control the amount of console output, making it easy to run silently or in debug mode.

External Configuration
All settings (SharePoint credentials, SQL connection string) are stored in an XML file, which can be overridden via command-line argument.

Prerequisites
Windows operating system (the application targets .NET Framework 4.8)

.NET Framework 4.8 (or later) – Download

Visual Studio 2019 or 2022 (for building from source)

Access to a SharePoint Online tenant with appropriate permissions to read/write lists

SQL Server (any edition) with a database where the target tables will be created/updated

VPN if your SharePoint or SQL Server is only accessible within a corporate network

Installation
Clone the repository

bash
git clone https://github.com/your-org/sharepoint-sync-tool.git
Open the solution in Visual Studio
ConsoleApp1.sln

Restore NuGet packages
The main package is Microsoft.SharePointOnline.CSOM (version 16.1).
Visual Studio should restore it automatically; if not, run:

text
Update-Package -reinstall
Build the solution (Build → Build Solution)
The executable will be placed in bin\Debug or bin\Release.

(Optional) Publish – you can copy the bin\Release folder to any Windows machine with .NET Framework 4.8 installed.

Configuration
All runtime settings are defined in an XML file. The default location is XmlConfig\UserConfig.xml (relative to the executable). You can specify a different file with the --config argument.

Example UserConfig.xml
xml
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
Security Note: Storing passwords in plain text is not recommended for production. Consider using encrypted configuration sections or environment variables.

Configuration Elements
Element	Description
SharePoint/Username	The SharePoint Online user account (email format).
SharePoint/Password	The password for that account.
SQL/ConnectionString	A valid ADO.NET connection string to your SQL Server database.
The tool expects specific SharePoint site relative paths (e.g., seed, wolf, selfservice/invoicerequest) – these are hard-coded in the data quality classes. If your site structure differs, you will need to adjust the source code.

Usage
Run the executable from the command line:

text
ConsoleApp1.exe [arguments]
Arguments
Argument	Description
daily	Perform incremental sync (typically only new/changed items).
monthly	Perform a full sync (all items).
diagnostic	Enable diagnostic mode (sets verbosity to 1 if not otherwise specified).
--verbose=<0-3>	Set verbosity level: 0 = quiet, 1 = normal, 2 = detailed, 3 = very detailed (debug).
--config=<path>	Use an alternative configuration file.
Examples
bash
# Daily sync with normal logging, default config
ConsoleApp1.exe daily

# Monthly sync with detailed logging and custom config
ConsoleApp1.exe monthly --verbose=2 --config="C:\Configs\custom.xml"

# Just test SQL connection and exit (diagnostic mode)
ConsoleApp1.exe diagnostic
What happens during execution?
The tool parses command-line arguments and sets up logging.

It tests the SQL Server connection (including permission checks).

SharePoint credentials are retrieved from the configuration.

Based on the mode (daily or monthly), it calls RefreshSQLLists.SPOtoSQLUpdate(...) which performs the actual data transfer.

Data quality routines (ActivitiesDQ, InvoiceRequestDQ, etc.) are executed as part of the sync process.

All actions are logged to the console according to the verbosity level.

Project Structure
text
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
Key components:

RefreshSPOLists – main entry point (Main method), argument handling, SQL test, and orchestration.

SPOList / SPOUser – wrappers for SharePoint CSOM operations.

ActivitiesDQ / InvoiceRequestDQ – data quality fixes for specific lists.

RefreshSQLLists – handles the actual SharePoint-to-SQL data transfer.

ConfigHelper – reads the XML configuration file.

Logger – simple console logger with verbosity levels.

Troubleshooting
Problem	Possible Solution
Cannot connect to SQL Server	Check VPN, firewall rules, and the connection string. Run diagnostic mode for more details.
SharePoint login fails	Verify username/password in config. Ensure the account has access to the specified site.
"Field not found" errors	The data quality classes expect specific field names (e.g., OpportunityID). If your lists use different names, you must modify the code.
Throttling / slow performance	The tool batches updates (e.g., 80 items per batch) to avoid SharePoint limits. Adjust batch sizes if needed.
Missing tables in SQL	The tool assumes tables already exist with the correct schema. Review SQLInteraction.cs for table creation logic.
If you encounter unexpected behaviour, run with --verbose=3 to get detailed debug output and open an issue with the log.

License
This project is licensed under the MIT License – see the LICENSE file for details.
