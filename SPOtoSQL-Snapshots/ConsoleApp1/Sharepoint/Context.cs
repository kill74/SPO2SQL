using Microsoft.SharePoint.Client;
using System;
using System.Linq.Expressions;
using System.Net;

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
            // Combine base URL with relative site path
            var url = "https://bringglobal.sharepoint.com/" + this.Site;

            // Create a new ClientContext and assign credentials
            var clientContext = new ClientContext(url)
            {
                Credentials = this.SPOUser.Credentials
            };

            // Store references for later use
            this.Ctx = clientContext;
            this.web = this.Ctx.Web;

            // Load minimal Web object metadata (e.g., Title, Url)
            this.Ctx.Load<Web>(this.web, Array.Empty<Expression<Func<Web, object>>>());
        }

        /// <summary>
        /// Retrieves all lists in the current Web context, reinitializing context if needed.
        /// </summary>
        /// <returns>ListCollection representing all lists on the site.</returns>
        public ListCollection GetAllLists()
        {
            // Ensure context is built or rebuilt if Site has changed
            var expectedUrl = "https://bringglobal.sharepoint.com/" + this.Site;
            if (this.web == null || this.Ctx.Site.Context.Url != expectedUrl)
            {
                this.BuildContext();
            }

            // Load and execute query to get all lists
            var lists = this.web.Lists;
            this.Ctx.Load<ListCollection>(lists, Array.Empty<Expression<Func<ListCollection, object>>>());
            this.Ctx.ExecuteQuery();
            return lists;
        }
    }
}