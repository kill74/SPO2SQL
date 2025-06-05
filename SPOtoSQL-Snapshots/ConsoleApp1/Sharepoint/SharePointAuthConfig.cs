using System;

namespace Bring.Sharepoint
{
    /// <summary>
    /// Configuration class for SharePoint authentication methods
    /// </summary>
    public class SharePointAuthConfig
    {
        /// <summary>
        /// Determines whether to use App Registration authentication
        /// </summary>
        public bool UseAppRegistration { get; set; }

        /// <summary>
        /// Azure AD Application (client) ID
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        /// Azure AD Client Secret
        /// </summary>
        public string ClientSecret { get; set; }

        /// <summary>
        /// Azure AD Tenant ID
        /// </summary>
        public string TenantId { get; set; }

        /// <summary>
        /// SharePoint username for credential authentication
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// SharePoint password for credential authentication
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// SharePoint site URL
        /// </summary>
        public string SiteUrl { get; set; }
    }
}