#!/bin/bash
#############################################################################
# SharePoint to SQL Sync Tool - User Secrets Setup
# 
# This script helps you configure User Secrets for development.
# It validates inputs and sets all required secrets securely.
#
# Usage: ./setup-secrets.sh
#############################################################################

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Project directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$SCRIPT_DIR/../ConsoleApp1Net8"

#############################################################################
# Helper Functions
#############################################################################

print_header() {
    echo -e "${BLUE}"
    echo "╔════════════════════════════════════════════════════════════════╗"
    echo "║     SharePoint to SQL Sync - User Secrets Setup (Dev)         ║"
    echo "╚════════════════════════════════════════════════════════════════╝"
    echo -e "${NC}"
}

print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠ $1${NC}"
}

print_error() {
    echo -e "${RED}✗ $1${NC}"
}

print_info() {
    echo -e "${BLUE}ℹ $1${NC}"
}

#############################################################################
# Validation Functions
#############################################################################

validate_email() {
    local email="$1"
    if [[ "$email" =~ ^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$ ]]; then
        return 0
    else
        return 1
    fi
}

validate_sharepoint_url() {
    local url="$1"
    if [[ "$url" =~ ^https://[a-zA-Z0-9.-]+\.sharepoint\.com/sites/[a-zA-Z0-9._-]+$ ]]; then
        return 0
    else
        return 1
    fi
}

validate_sql_connection_string() {
    local conn_str="$1"
    
    # Check for required components
    if [[ ! "$conn_str" =~ Server=.+ ]]; then
        print_error "Connection string must contain 'Server=' parameter"
        return 1
    fi
    
    if [[ ! "$conn_str" =~ Database=.+ ]]; then
        print_error "Connection string must contain 'Database=' parameter"
        return 1
    fi
    
    # Check for authentication method
    if [[ ! "$conn_str" =~ "Integrated Security=true" ]] && \
       [[ ! "$conn_str" =~ "User Id=".+ ]]; then
        print_error "Connection string must contain either 'Integrated Security=true' or 'User Id=' for authentication"
        return 1
    fi
    
    # Check for encryption (security best practice)
    if [[ ! "$conn_str" =~ "Encrypt=true" ]]; then
        print_warning "Connection string should contain 'Encrypt=true' for security"
        echo -n "Continue anyway? (y/N): "
        read -r response
        if [[ ! "$response" =~ ^[Yy]$ ]]; then
            return 1
        fi
    fi
    
    return 0
}

check_dotnet() {
    if ! command -v dotnet &> /dev/null; then
        print_error ".NET SDK is not installed or not in PATH"
        print_info "Install .NET 8 SDK from: https://dotnet.microsoft.com/download"
        exit 1
    fi
    
    local dotnet_version=$(dotnet --version)
    print_success ".NET SDK version $dotnet_version found"
}

check_project() {
    if [ ! -f "$PROJECT_DIR/ConsoleApp1Net8.csproj" ]; then
        print_error "Project file not found at: $PROJECT_DIR/ConsoleApp1Net8.csproj"
        print_info "Make sure you run this script from the scripts directory"
        exit 1
    fi
    
    print_success "Project found: $PROJECT_DIR"
}

#############################################################################
# User Secrets Management
#############################################################################

initialize_secrets() {
    print_info "Initializing User Secrets..."
    
    cd "$PROJECT_DIR"
    
    # Check if already initialized
    if dotnet user-secrets list &> /dev/null; then
        print_warning "User Secrets already initialized"
    else
        dotnet user-secrets init
        print_success "User Secrets initialized"
    fi
    
    cd - > /dev/null
}

list_current_secrets() {
    print_info "Current User Secrets:"
    cd "$PROJECT_DIR"
    
    local secrets=$(dotnet user-secrets list 2>&1)
    if [ $? -eq 0 ]; then
        if [ -z "$secrets" ] || [[ "$secrets" == "No secrets configured"* ]]; then
            print_warning "No secrets currently configured"
        else
            echo "$secrets" | while read -r line; do
                # Mask passwords
                if [[ "$line" =~ Password ]]; then
                    echo "$line" | sed 's/=.*/= ********/'
                else
                    echo "$line"
                fi
            done
        fi
    else
        print_error "Failed to list secrets: $secrets"
    fi
    
    cd - > /dev/null
}

set_secret() {
    local key="$1"
    local value="$2"
    
    cd "$PROJECT_DIR"
    
    if dotnet user-secrets set "$key" "$value" > /dev/null 2>&1; then
        print_success "Set: $key"
        return 0
    else
        print_error "Failed to set: $key"
        return 1
    fi
    
    cd - > /dev/null
}

#############################################################################
# Interactive Configuration
#############################################################################

configure_sharepoint() {
    echo ""
    echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${BLUE}SharePoint Configuration${NC}"
    echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    
    # SharePoint Username
    while true; do
        echo ""
        echo -n "SharePoint Username (email): "
        read -r sp_username
        
        if [ -z "$sp_username" ]; then
            print_error "Username cannot be empty"
            continue
        fi
        
        if validate_email "$sp_username"; then
            break
        else
            print_error "Invalid email format. Please enter a valid email address."
        fi
    done
    
    # SharePoint Password
    while true; do
        echo ""
        echo -n "SharePoint Password: "
        read -rs sp_password
        echo ""
        
        if [ -z "$sp_password" ]; then
            print_error "Password cannot be empty"
            continue
        fi
        
        if [ ${#sp_password} -lt 8 ]; then
            print_warning "Password is less than 8 characters. This may not meet security requirements."
            echo -n "Continue anyway? (y/N): "
            read -r response
            if [[ ! "$response" =~ ^[Yy]$ ]]; then
                continue
            fi
        fi
        
        break
    done
    
    # SharePoint Site URL
    while true; do
        echo ""
        echo "SharePoint Site URL (e.g., https://company.sharepoint.com/sites/yoursite): "
        read -r sp_url
        
        if [ -z "$sp_url" ]; then
            print_error "Site URL cannot be empty"
            continue
        fi
        
        if validate_sharepoint_url "$sp_url"; then
            break
        else
            print_warning "URL format doesn't match expected pattern (https://xxx.sharepoint.com/sites/xxx)"
            echo -n "Continue anyway? (y/N): "
            read -r response
            if [[ ! "$response" =~ ^[Yy]$ ]]; then
                continue
            fi
            break
        fi
    done
    
    # Set secrets
    set_secret "SharePoint:Username" "$sp_username"
    set_secret "SharePoint:Password" "$sp_password"
    set_secret "SharePoint:SiteUrl" "$sp_url"
}

configure_sql() {
    echo ""
    echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${BLUE}SQL Server Configuration${NC}"
    echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    
    echo ""
    print_info "Choose authentication method:"
    echo "  1) Windows Integrated Security (Recommended for domain-joined dev machine)"
    echo "  2) SQL Server Authentication (Username/Password)"
    echo ""
    echo -n "Enter choice (1 or 2): "
    read -r auth_choice
    
    # Server name
    echo ""
    echo -n "SQL Server name (e.g., localhost, myserver.database.windows.net): "
    read -r sql_server
    
    if [ -z "$sql_server" ]; then
        print_error "Server name cannot be empty"
        configure_sql
        return
    fi
    
    # Database name
    echo -n "Database name: "
    read -r sql_database
    
    if [ -z "$sql_database" ]; then
        print_error "Database name cannot be empty"
        configure_sql
        return
    fi
    
    # Build connection string based on auth method
    if [ "$auth_choice" == "1" ]; then
        sql_conn_str="Server=$sql_server;Database=$sql_database;Integrated Security=true;Encrypt=true;TrustServerCertificate=false;"
    elif [ "$auth_choice" == "2" ]; then
        echo -n "SQL Username: "
        read -r sql_user
        
        echo -n "SQL Password: "
        read -rs sql_password
        echo ""
        
        if [ -z "$sql_user" ] || [ -z "$sql_password" ]; then
            print_error "Username and password cannot be empty"
            configure_sql
            return
        fi
        
        sql_conn_str="Server=$sql_server;Database=$sql_database;User Id=$sql_user;Password=$sql_password;Encrypt=true;TrustServerCertificate=false;"
    else
        print_error "Invalid choice. Please select 1 or 2."
        configure_sql
        return
    fi
    
    # Validate connection string
    echo ""
    print_info "Connection string: $(echo "$sql_conn_str" | sed 's/Password=[^;]*/Password=********/g')"
    
    if validate_sql_connection_string "$sql_conn_str"; then
        set_secret "Sql:ConnectionString" "$sql_conn_str"
    else
        print_error "Connection string validation failed"
        echo -n "Retry SQL configuration? (Y/n): "
        read -r response
        if [[ ! "$response" =~ ^[Nn]$ ]]; then
            configure_sql
        fi
    fi
}

#############################################################################
# Main Flow
#############################################################################

main() {
    print_header
    
    # Pre-flight checks
    check_dotnet
    check_project
    
    echo ""
    print_info "This script will help you configure User Secrets for development."
    print_warning "User Secrets are stored locally and NOT committed to source control."
    print_warning "For production, use Environment Variables instead."
    
    echo ""
    echo -n "Continue with configuration? (Y/n): "
    read -r response
    
    if [[ "$response" =~ ^[Nn]$ ]]; then
        print_info "Configuration cancelled."
        exit 0
    fi
    
    # Initialize User Secrets
    initialize_secrets
    
    echo ""
    list_current_secrets
    
    echo ""
    echo -n "Do you want to configure SharePoint settings? (Y/n): "
    read -r response
    if [[ ! "$response" =~ ^[Nn]$ ]]; then
        configure_sharepoint
    fi
    
    echo ""
    echo -n "Do you want to configure SQL Server settings? (Y/n): "
    read -r response
    if [[ ! "$response" =~ ^[Nn]$ ]]; then
        configure_sql
    fi
    
    # Summary
    echo ""
    echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${GREEN}Configuration Complete!${NC}"
    echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    
    echo ""
    list_current_secrets
    
    echo ""
    print_info "Next steps:"
    echo "  1. Verify configuration: ./scripts/verify-config.sh"
    echo "  2. Run the application: cd ConsoleApp1Net8 && dotnet run"
    echo ""
    print_info "To update secrets later:"
    echo "  - Run this script again, or"
    echo "  - Use: dotnet user-secrets set \"<key>\" \"<value>\""
    echo ""
    print_success "Setup complete!"
}

# Run main
main
