using System;

namespace SPO2SQL.SharePoint;

public class SharePointAuthConfig
{
    public bool UseAppRegistration { get; set; }

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string TenantId { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string SiteUrl { get; set; } = string.Empty;
}
