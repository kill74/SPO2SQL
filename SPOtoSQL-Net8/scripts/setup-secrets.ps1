#############################################################################
# SharePoint to SQL Sync Tool - User Secrets Setup (PowerShell)
# 
# This script helps you configure User Secrets for development.
# It validates inputs and sets all required secrets securely.
#
# Usage: .\setup-secrets.ps1
#############################################################################

$ErrorActionPreference = "Stop"

# Project directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = Join-Path $ScriptDir "..\ConsoleApp1Net8"

#############################################################################
# Helper Functions
#############################################################################

function Write-Header {
    Write-Host ""
    Write-Host "╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║     SharePoint to SQL Sync - User Secrets Setup (Dev)         ║" -ForegroundColor Cyan
    Write-Host "╚════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host ""
}

function Write-Success {
    param([string]$Message)
    Write-Host "✓ $Message" -ForegroundColor Green
}

function Write-Warning {
    param([string]$Message)
    Write-Host "⚠ $Message" -ForegroundColor Yellow
}

function Write-Error {
    param([string]$Message)
    Write-Host "✗ $Message" -ForegroundColor Red
}

function Write-Info {
    param([string]$Message)
    Write-Host "ℹ $Message" -ForegroundColor Cyan
}

#############################################################################
# Validation Functions
#############################################################################

function Test-Email {
    param([string]$Email)
    
    $emailRegex = '^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$'
    return $Email -match $emailRegex
}

function Test-SharePointUrl {
    param([string]$Url)
    
    $urlRegex = '^https://[a-zA-Z0-9.-]+\.sharepoint\.com/sites/[a-zA-Z0-9._-]+$'
    return $Url -match $urlRegex
}

function Test-SqlConnectionString {
    param([string]$ConnectionString)
    
    # Check for required components
    if ($ConnectionString -notmatch 'Server=.+') {
        Write-Error "Connection string must contain 'Server=' parameter"
        return $false
    }
    
    if ($ConnectionString -notmatch 'Database=.+') {
        Write-Error "Connection string must contain 'Database=' parameter"
        return $false
    }
    
    # Check for authentication method
    if (($ConnectionString -notmatch 'Integrated Security=true') -and 
        ($ConnectionString -notmatch 'User Id=.+')) {
        Write-Error "Connection string must contain either 'Integrated Security=true' or 'User Id=' for authentication"
        return $false
    }
    
    # Check for encryption (security best practice)
    if ($ConnectionString -notmatch 'Encrypt=true') {
        Write-Warning "Connection string should contain 'Encrypt=true' for security"
        $response = Read-Host "Continue anyway? (y/N)"
        if ($response -ne 'y' -and $response -ne 'Y') {
            return $false
        }
    }
    
    return $true
}

function Test-DotnetSdk {
    try {
        $dotnetVersion = & dotnet --version
        Write-Success ".NET SDK version $dotnetVersion found"
        return $true
    }
    catch {
        Write-Error ".NET SDK is not installed or not in PATH"
        Write-Info "Install .NET 8 SDK from: https://dotnet.microsoft.com/download"
        return $false
    }
}

function Test-Project {
    $projectFile = Join-Path $ProjectDir "ConsoleApp1Net8.csproj"
    
    if (-not (Test-Path $projectFile)) {
        Write-Error "Project file not found at: $projectFile"
        Write-Info "Make sure you run this script from the scripts directory"
        return $false
    }
    
    Write-Success "Project found: $ProjectDir"
    return $true
}

#############################################################################
# User Secrets Management
#############################################################################

function Initialize-UserSecrets {
    Write-Info "Initializing User Secrets..."
    
    Push-Location $ProjectDir
    
    try {
        $null = & dotnet user-secrets list 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Warning "User Secrets already initialized"
        }
        else {
            & dotnet user-secrets init
            Write-Success "User Secrets initialized"
        }
    }
    finally {
        Pop-Location
    }
}

function Get-CurrentSecrets {
    Write-Info "Current User Secrets:"
    
    Push-Location $ProjectDir
    
    try {
        $secrets = & dotnet user-secrets list 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            if ([string]::IsNullOrWhiteSpace($secrets) -or $secrets -like "No secrets configured*") {
                Write-Warning "No secrets currently configured"
            }
            else {
                $secrets -split "`n" | ForEach-Object {
                    # Mask passwords
                    if ($_ -match 'Password') {
                        $_ -replace '=.*', '= ********'
                    }
                    else {
                        $_
                    }
                } | Write-Host
            }
        }
        else {
            Write-Error "Failed to list secrets: $secrets"
        }
    }
    finally {
        Pop-Location
    }
}

function Set-UserSecret {
    param(
        [string]$Key,
        [string]$Value
    )
    
    Push-Location $ProjectDir
    
    try {
        $null = & dotnet user-secrets set $Key $Value 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Set: $Key"
            return $true
        }
        else {
            Write-Error "Failed to set: $Key"
            return $false
        }
    }
    finally {
        Pop-Location
    }
}

#############################################################################
# Interactive Configuration
#############################################################################

function Set-SharePointConfiguration {
    Write-Host ""
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
    Write-Host "SharePoint Configuration" -ForegroundColor Cyan
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
    
    # SharePoint Username
    do {
        Write-Host ""
        $spUsername = Read-Host "SharePoint Username (email)"
        
        if ([string]::IsNullOrWhiteSpace($spUsername)) {
            Write-Error "Username cannot be empty"
            continue
        }
        
        if (-not (Test-Email $spUsername)) {
            Write-Error "Invalid email format. Please enter a valid email address."
            continue
        }
        
        break
    } while ($true)
    
    # SharePoint Password
    do {
        Write-Host ""
        $spPasswordSecure = Read-Host "SharePoint Password" -AsSecureString
        $spPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
            [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($spPasswordSecure))
        
        if ([string]::IsNullOrWhiteSpace($spPassword)) {
            Write-Error "Password cannot be empty"
            continue
        }
        
        if ($spPassword.Length -lt 8) {
            Write-Warning "Password is less than 8 characters. This may not meet security requirements."
            $response = Read-Host "Continue anyway? (y/N)"
            if ($response -ne 'y' -and $response -ne 'Y') {
                continue
            }
        }
        
        break
    } while ($true)
    
    # SharePoint Site URL
    do {
        Write-Host ""
        $spUrl = Read-Host "SharePoint Site URL (e.g., https://company.sharepoint.com/sites/yoursite)"
        
        if ([string]::IsNullOrWhiteSpace($spUrl)) {
            Write-Error "Site URL cannot be empty"
            continue
        }
        
        if (-not (Test-SharePointUrl $spUrl)) {
            Write-Warning "URL format doesn't match expected pattern (https://xxx.sharepoint.com/sites/xxx)"
            $response = Read-Host "Continue anyway? (y/N)"
            if ($response -ne 'y' -and $response -ne 'Y') {
                continue
            }
        }
        
        break
    } while ($true)
    
    # Set secrets
    Set-UserSecret "SharePoint:Username" $spUsername
    Set-UserSecret "SharePoint:Password" $spPassword
    Set-UserSecret "SharePoint:SiteUrl" $spUrl
}

function Set-SqlConfiguration {
    Write-Host ""
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
    Write-Host "SQL Server Configuration" -ForegroundColor Cyan
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
    
    Write-Host ""
    Write-Info "Choose authentication method:"
    Write-Host "  1) Windows Integrated Security (Recommended for domain-joined dev machine)"
    Write-Host "  2) SQL Server Authentication (Username/Password)"
    Write-Host ""
    
    do {
        $authChoice = Read-Host "Enter choice (1 or 2)"
        if ($authChoice -eq '1' -or $authChoice -eq '2') {
            break
        }
        Write-Error "Invalid choice. Please select 1 or 2."
    } while ($true)
    
    # Server name
    Write-Host ""
    do {
        $sqlServer = Read-Host "SQL Server name (e.g., localhost, myserver.database.windows.net)"
        if (-not [string]::IsNullOrWhiteSpace($sqlServer)) {
            break
        }
        Write-Error "Server name cannot be empty"
    } while ($true)
    
    # Database name
    do {
        $sqlDatabase = Read-Host "Database name"
        if (-not [string]::IsNullOrWhiteSpace($sqlDatabase)) {
            break
        }
        Write-Error "Database name cannot be empty"
    } while ($true)
    
    # Build connection string based on auth method
    if ($authChoice -eq '1') {
        $sqlConnStr = "Server=$sqlServer;Database=$sqlDatabase;Integrated Security=true;Encrypt=true;TrustServerCertificate=false;"
    }
    elseif ($authChoice -eq '2') {
        do {
            $sqlUser = Read-Host "SQL Username"
            if (-not [string]::IsNullOrWhiteSpace($sqlUser)) {
                break
            }
            Write-Error "Username cannot be empty"
        } while ($true)
        
        do {
            $sqlPasswordSecure = Read-Host "SQL Password" -AsSecureString
            $sqlPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
                [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($sqlPasswordSecure))
            
            if (-not [string]::IsNullOrWhiteSpace($sqlPassword)) {
                break
            }
            Write-Error "Password cannot be empty"
        } while ($true)
        
        $sqlConnStr = "Server=$sqlServer;Database=$sqlDatabase;User Id=$sqlUser;Password=$sqlPassword;Encrypt=true;TrustServerCertificate=false;"
    }
    
    # Validate connection string
    Write-Host ""
    $maskedConnStr = $sqlConnStr -replace 'Password=[^;]*', 'Password=********'
    Write-Info "Connection string: $maskedConnStr"
    
    if (Test-SqlConnectionString $sqlConnStr) {
        Set-UserSecret "Sql:ConnectionString" $sqlConnStr
    }
    else {
        Write-Error "Connection string validation failed"
        $response = Read-Host "Retry SQL configuration? (Y/n)"
        if ($response -ne 'n' -and $response -ne 'N') {
            Set-SqlConfiguration
        }
    }
}

#############################################################################
# Main Flow
#############################################################################

function Main {
    Write-Header
    
    # Pre-flight checks
    if (-not (Test-DotnetSdk)) {
        exit 1
    }
    
    if (-not (Test-Project)) {
        exit 1
    }
    
    Write-Host ""
    Write-Info "This script will help you configure User Secrets for development."
    Write-Warning "User Secrets are stored locally and NOT committed to source control."
    Write-Warning "For production, use Environment Variables instead."
    
    Write-Host ""
    $response = Read-Host "Continue with configuration? (Y/n)"
    
    if ($response -eq 'n' -or $response -eq 'N') {
        Write-Info "Configuration cancelled."
        exit 0
    }
    
    # Initialize User Secrets
    Initialize-UserSecrets
    
    Write-Host ""
    Get-CurrentSecrets
    
    Write-Host ""
    $response = Read-Host "Do you want to configure SharePoint settings? (Y/n)"
    if ($response -ne 'n' -and $response -ne 'N') {
        Set-SharePointConfiguration
    }
    
    Write-Host ""
    $response = Read-Host "Do you want to configure SQL Server settings? (Y/n)"
    if ($response -ne 'n' -and $response -ne 'N') {
        Set-SqlConfiguration
    }
    
    # Summary
    Write-Host ""
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Green
    Write-Host "Configuration Complete!" -ForegroundColor Green
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Green
    
    Write-Host ""
    Get-CurrentSecrets
    
    Write-Host ""
    Write-Info "Next steps:"
    Write-Host "  1. Verify configuration: .\scripts\verify-config.sh"
    Write-Host "  2. Run the application: cd ConsoleApp1Net8; dotnet run"
    Write-Host ""
    Write-Info "To update secrets later:"
    Write-Host "  - Run this script again, or"
    Write-Host "  - Use: dotnet user-secrets set `"<key>`" `"<value>`""
    Write-Host ""
    Write-Success "Setup complete!"
}

# Run main
Main
