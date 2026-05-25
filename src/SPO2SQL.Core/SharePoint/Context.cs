using System;
using System.Linq.Expressions;
using System.Net;
using Microsoft.SharePoint.Client;
using SPO2SQL.Logging;
using SPO2SQL.XmlConfig;

namespace SPO2SQL.SharePoint;

public class Context : IDisposable
{
    internal Web web;

    public string Site { get; set; }

    public SPOUser SPOUser { get; set; }

    public ClientContext Ctx { get; set; }

    public void BuildContext()
    {
        try
        {
            string baseUrl = ConfigurationReader.GetSharePointBaseUrl()
                ?? "https://bringglobal.sharepoint.com";

            string url = $"{baseUrl.TrimEnd('/')}/{Site?.TrimStart('/') ?? throw new InvalidOperationException("Site is not set. Set the Site property before building context.")}";
            Logger.LogDebug($"Building SharePoint context for: {url}");

            var clientContext = new ClientContext(url);
            ArgumentNullException.ThrowIfNull(SPOUser);
            SPOUser.ApplyAuthentication(clientContext, url);

            Ctx = clientContext;
            web = Ctx.Web;

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

    public void Dispose()
    {
        Ctx?.Dispose();
        GC.SuppressFinalize(this);
    }

    public ListCollection GetAllLists()
    {
        try
        {
            string baseUrl = ConfigurationReader.GetSharePointBaseUrl()
                ?? "https://bringglobal.sharepoint.com";
            string expectedUrl = $"{baseUrl.TrimEnd('/')}/{Site?.TrimStart('/') ?? string.Empty}";

            if (web == null || Ctx?.Url != expectedUrl)
            {
                Logger.LogDebug("Rebuilding context for GetAllLists");
                BuildContext();
            }

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
