#!/bin/bash
#############################################################################
# SharePoint to SQL Sync Tool - Configuration Verification
# 
# This script verifies that all required configuration is present and valid.
# It checks User Secrets (dev) and Environment Variables (prod) without
# exposing sensitive credentials.
#
# Usage: ./verify-config.sh [--env-vars]
#        --env-vars: Check environment variables instead of user secrets
#############################################################################

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# Flags
CHECK_ENV_VARS=false

# Project directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$SCRIPT_DIR/../ConsoleApp1Net8"

#############################################################################
# Helper Functions
#############################################################################

print_header() {
    echo -e "${BLUE}"
    echo "╔════════════════════════════════════════════════════════════════╗"
    echo "║         SharePoint to SQL Sync - Config Verification          ║"
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
# Configuration Checks
#############################################################################

check_required_config() {
    local key="$1"
    local value="$2"
    local description="$3"
    
    if [ -z "$value" ]; then
        print_error "Missing: $description ($key)"
        return 1
    else
        print_success "$description is set"
        return 0
    fi
}

validate_email() {
    local email="$1"
    if [[ "$email" =~ ^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$ ]]; then
        return 0
    else
        return 1
    fi
}

validate_url() {
    local url="$1"
    if [[ "$url" =~ ^https?:// ]]; then
        return 0
    else
        return 1
    fi
}

validate_connection_string() {
    local conn_str="$1"
    local issues=0
    
    # Check for Server
    if [[ ! "$conn_str" =~ Server=.+ ]]; then
        print_error "  Missing 'Server=' in connection string"
        issues=$((issues + 1))
    fi
    
    # Check for Database
    if [[ ! "$conn_str" =~ Database=.+ ]]; then
        print_error "  Missing 'Database=' in connection string"
        issues=$((issues + 1))
    fi
    
    # Check for authentication
    if [[ ! "$conn_str" =~ "Integrated Security=true" ]] && \
       [[ ! "$conn_str" =~ "User Id=".+ ]]; then
        print_error "  Missing authentication (need 'Integrated Security=true' or 'User Id=')"
        issues=$((issues + 1))
    fi
    
    # Check for encryption
    if [[ ! "$conn_str" =~ "Encrypt=true" ]]; then
        print_warning "  Missing 'Encrypt=true' (recommended for security)"
    fi
    
    # Check for TrustServerCertificate
    if [[ "$conn_str" =~ "TrustServerCertificate=true" ]]; then
        print_warning "  'TrustServerCertificate=true' is not recommended for production"
    fi
    
    return $issues
}

#############################################################################
# User Secrets Verification
#############################################################################

verify_user_secrets() {
    print_info "Checking User Secrets configuration..."
    echo ""
    
    if [ ! -f "$PROJECT_DIR/ConsoleApp1Net8.csproj" ]; then
        print_error "Project file not found: $PROJECT_DIR/ConsoleApp1Net8.csproj"
        return 1
    fi
    
    cd "$PROJECT_DIR"
    
    # Check if user secrets are initialized
    if ! dotnet user-secrets list &> /dev/null; then
        print_error "User Secrets not initialized"
        print_info "Run: ./scripts/setup-secrets.sh"
        cd - > /dev/null
        return 1
    fi
    
    # Get all secrets
    local secrets=$(dotnet user-secrets list 2>&1)
    
    if [ -z "$secrets" ] || [[ "$secrets" == "No secrets configured"* ]]; then
        print_error "No secrets configured"
        print_info "Run: ./scripts/setup-secrets.sh"
        cd - > /dev/null
        return 1
    fi
    
    # Parse secrets into associative array
    declare -A config
    while IFS='=' read -r key value; do
        # Trim whitespace
        key=$(echo "$key" | xargs)
        value=$(echo "$value" | xargs)
        config["$key"]="$value"
    done <<< "$secrets"
    
    local errors=0
    
    # Check SharePoint configuration
    echo -e "${BLUE}SharePoint Configuration:${NC}"
    
    check_required_config "SharePoint:Username" "${config[SharePoint:Username]}" "SharePoint Username" || errors=$((errors + 1))
    
    if [ -n "${config[SharePoint:Username]}" ]; then
        if validate_email "${config[SharePoint:Username]}"; then
            print_success "  Email format is valid"
        else
            print_warning "  Email format may be invalid"
        fi
    fi
    
    check_required_config "SharePoint:Password" "${config[SharePoint:Password]}" "SharePoint Password" || errors=$((errors + 1))
    
    if [ -n "${config[SharePoint:Password]}" ]; then
        local pwd_len=${#config[SharePoint:Password]}
        if [ $pwd_len -lt 8 ]; then
            print_warning "  Password is less than 8 characters"
        else
            print_success "  Password length is adequate ($pwd_len characters)"
        fi
    fi
    
    check_required_config "SharePoint:SiteUrl" "${config[SharePoint:SiteUrl]}" "SharePoint Site URL" || errors=$((errors + 1))
    
    if [ -n "${config[SharePoint:SiteUrl]}" ]; then
        if validate_url "${config[SharePoint:SiteUrl]}"; then
            print_success "  URL format is valid"
        else
            print_error "  URL must start with https://"
            errors=$((errors + 1))
        fi
    fi
    
    # Check SQL configuration
    echo ""
    echo -e "${BLUE}SQL Server Configuration:${NC}"
    
    check_required_config "Sql:ConnectionString" "${config[Sql:ConnectionString]}" "SQL Connection String" || errors=$((errors + 1))
    
    if [ -n "${config[Sql:ConnectionString]}" ]; then
        validate_connection_string "${config[Sql:ConnectionString]}" || errors=$((errors + $?))
    fi
    
    cd - > /dev/null
    
    return $errors
}

#############################################################################
# Environment Variables Verification
#############################################################################

verify_env_vars() {
    print_info "Checking Environment Variables configuration..."
    echo ""
    
    local errors=0
    
    # Check SharePoint configuration
    echo -e "${BLUE}SharePoint Configuration:${NC}"
    
    check_required_config "SPO2SQL_SharePoint__Username" "$SPO2SQL_SharePoint__Username" "SharePoint Username" || errors=$((errors + 1))
    
    if [ -n "$SPO2SQL_SharePoint__Username" ]; then
        if validate_email "$SPO2SQL_SharePoint__Username"; then
            print_success "  Email format is valid"
        else
            print_warning "  Email format may be invalid"
        fi
    fi
    
    check_required_config "SPO2SQL_SharePoint__Password" "$SPO2SQL_SharePoint__Password" "SharePoint Password" || errors=$((errors + 1))
    
    if [ -n "$SPO2SQL_SharePoint__Password" ]; then
        local pwd_len=${#SPO2SQL_SharePoint__Password}
        if [ $pwd_len -lt 8 ]; then
            print_warning "  Password is less than 8 characters"
        else
            print_success "  Password length is adequate ($pwd_len characters)"
        fi
    fi
    
    check_required_config "SPO2SQL_SharePoint__SiteUrl" "$SPO2SQL_SharePoint__SiteUrl" "SharePoint Site URL" || errors=$((errors + 1))
    
    if [ -n "$SPO2SQL_SharePoint__SiteUrl" ]; then
        if validate_url "$SPO2SQL_SharePoint__SiteUrl"; then
            print_success "  URL format is valid"
        else
            print_error "  URL must start with https://"
            errors=$((errors + 1))
        fi
    fi
    
    # Check SQL configuration
    echo ""
    echo -e "${BLUE}SQL Server Configuration:${NC}"
    
    check_required_config "SPO2SQL_Sql__ConnectionString" "$SPO2SQL_Sql__ConnectionString" "SQL Connection String" || errors=$((errors + 1))
    
    if [ -n "$SPO2SQL_Sql__ConnectionString" ]; then
        validate_connection_string "$SPO2SQL_Sql__ConnectionString" || errors=$((errors + $?))
    fi
    
    return $errors
}

#############################################################################
# appsettings.json Verification
#############################################################################

verify_appsettings() {
    print_info "Checking appsettings.json..."
    echo ""
    
    local appsettings="$PROJECT_DIR/appsettings.json"
    
    if [ ! -f "$appsettings" ]; then
        print_error "appsettings.json not found at: $appsettings"
        return 1
    fi
    
    print_success "appsettings.json found"
    
    # Check for secrets in appsettings.json (security check)
    local has_secrets=0
    
    if grep -qi '"Username"[[:space:]]*:[[:space:]]*"[^"]\+@' "$appsettings" 2>/dev/null; then
        print_error "⚠ SECURITY: Username found in appsettings.json - should be in User Secrets!"
        has_secrets=1
    fi
    
    if grep -qi '"Password"[[:space:]]*:[[:space:]]*"[^"]\+' "$appsettings" 2>/dev/null; then
        local pwd_value=$(grep -oi '"Password"[[:space:]]*:[[:space:]]*"[^"]*"' "$appsettings" | cut -d'"' -f4)
        if [ -n "$pwd_value" ]; then
            print_error "⚠ SECURITY: Password found in appsettings.json - should be in User Secrets!"
            has_secrets=1
        fi
    fi
    
    if grep -qi '"ConnectionString"[[:space:]]*:[[:space:]]*"Server=' "$appsettings" 2>/dev/null; then
        print_error "⚠ SECURITY: Connection string found in appsettings.json - should be in User Secrets!"
        has_secrets=1
    fi
    
    if [ $has_secrets -eq 0 ]; then
        print_success "No secrets found in appsettings.json (good!)"
    fi
    
    return $has_secrets
}

#############################################################################
# Main Flow
#############################################################################

parse_args() {
    while [ $# -gt 0 ]; do
        case "$1" in
            --env-vars)
                CHECK_ENV_VARS=true
                shift
                ;;
            --help|-h)
                echo "Usage: $0 [OPTIONS]"
                echo ""
                echo "Options:"
                echo "  --env-vars    Check environment variables instead of user secrets"
                echo "  --help, -h    Show this help message"
                echo ""
                echo "Examples:"
                echo "  $0                  # Verify user secrets (development)"
                echo "  $0 --env-vars       # Verify environment variables (production)"
                exit 0
                ;;
            *)
                print_error "Unknown option: $1"
                echo "Use --help for usage information"
                exit 1
                ;;
        esac
    done
}

main() {
    parse_args "$@"
    
    print_header
    
    local total_errors=0
    
    # Check appsettings.json
    verify_appsettings || total_errors=$((total_errors + $?))
    
    echo ""
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo ""
    
    # Check configuration source
    if [ "$CHECK_ENV_VARS" = true ]; then
        print_info "Mode: Production (Environment Variables)"
        echo ""
        verify_env_vars || total_errors=$((total_errors + $?))
    else
        print_info "Mode: Development (User Secrets)"
        echo ""
        verify_user_secrets || total_errors=$((total_errors + $?))
    fi
    
    # Summary
    echo ""
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo ""
    
    if [ $total_errors -eq 0 ]; then
        echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
        echo -e "${GREEN}✓ Configuration is valid!${NC}"
        echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
        echo ""
        print_info "You can now run the application:"
        echo "  cd ConsoleApp1Net8 && dotnet run"
        return 0
    else
        echo -e "${RED}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
        echo -e "${RED}✗ Configuration has $total_errors error(s)${NC}"
        echo -e "${RED}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
        echo ""
        print_info "To fix configuration:"
        if [ "$CHECK_ENV_VARS" = true ]; then
            echo "  - Set missing environment variables with SPO2SQL_ prefix"
            echo "  - Use double underscores __ for nested sections"
            echo "  Example: export SPO2SQL_SharePoint__Username=\"user@company.com\""
        else
            echo "  - Run: ./scripts/setup-secrets.sh"
            echo "  - Or manually: dotnet user-secrets set \"<key>\" \"<value>\""
        fi
        return 1
    fi
}

# Run main with all arguments
main "$@"
exit $?
