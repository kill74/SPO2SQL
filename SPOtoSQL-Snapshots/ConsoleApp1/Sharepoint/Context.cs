using Microsoft.SharePoint.Client;
using System;
using System.Linq.Expressions;
using System.Net;
using Bring.SPODataQuality;
using Bring.XmlConfig;

namespace Bring.Sharepoint
{
    /// <summary>
    /// Base class providing SharePoint client context initialization
    /// and utility methods for derived list operations.
    /// </summary>
    public class Context
    {
        /// <summary>
        /// Internal reference to the SharePoint Web object.
        /// </summary>
        internal Web web;

        /// <summary>
        /// Relative site path (e.g., "sites/mysite" or "selfservice/timesheet").
        /// </summary>
        public string Site { get; set; }

        /// <summary>
        /// Authenticated SharePoint Online user credentials.
        /// </summary>
        public SPOUser SPOUser { get; set; }

        /// <summary>
        /// The ClientContext used for executing queries against SharePoint.
        /// </summary>
        public ClientContext Ctx { get; set; }

        /// <summary>
        /// Builds or rebuilds the SharePoint client context for the configured Site and user.
        /// Must be called before executing any list/web operations.
        /// </summary>
        public void BuildContext()
        {
            try
            {
                // Get base URL from configuration, with fallback to default
                string baseUrl = ConfigurationReader.GetSharePointBaseUrl() 
                    ?? "https://bringglobal.sharepoint.com";
                
                string url = $"{baseUrl.TrimEnd('/')}/{Site.TrimStart('/')}";
                Logger.LogDebug($"Building SharePoint context for: {url}");

                // Create a new ClientContext and assign credentials
                var clientContext = new ClientContext(url)
                {
                    Credentials = SPOUser.Credentials
                };

                // Store references for later use
                Ctx = clientContext;
                web = Ctx.Web;

                // Load minimal Web object metadata
                Ctx.Load(web, w => w.Title, w => w.Url);
                Ctx.ExecuteQuery();

                Logger.LogDebug($"SharePoint context established successfully for site: {web.Title}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to build SharePoint context for site '{Site}'", ex);
                throw;
            }
        }

        /// <summary>
        /// Retrieves all lists in the current Web context, reinitializing context if needed.
        /// </summary>
        /// <returns>ListCollection representing all lists on the site.</returns>
        public ListCollection GetAllLists()
        {
            try
            {
                // Ensure context is built or rebuilt if Site has changed
                string baseUrl = ConfigurationReader.GetSharePointBaseUrl() 
                    ?? "https://bringglobal.sharepoint.com";
                string expectedUrl = $"{baseUrl.TrimEnd('/')}/{Site.TrimStart('/')}";
                
                if (web == null || Ctx?.Site?.Url != expectedUrl)
                {
                    Logger.LogDebug("Rebuilding context for GetAllLists");
                    BuildContext();
                }

                // Load and execute query to get all lists
                var lists = web.Lists;
                Ctx.Load(lists);
                Ctx.ExecuteQuery();
                
                Logger.LogDebug($"Retrieved {lists.Count} lists from site");
                return lists;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to retrieve lists from site '{Site}'", ex);
                throw;
            }
        }
    }
}