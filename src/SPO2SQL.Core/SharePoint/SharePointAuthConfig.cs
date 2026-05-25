using System;

namespace SPO2SQL.SharePoint;

public class SharePointAuthConfig
{
    public bool UseAppRegistration { get; set; }

    public string ClientId { get; set; }

    public string ClientSecret { get; set; }

    public string TenantId { get; set; }

    public string Username { get; set; }

    public string Password { get; set; }

    public string SiteUrl { get; set; }
}
