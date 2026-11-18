using Microsoft.SharePoint.Client;
using SPO2SQL.Logging;
using SPO2SQL.XmlConfig;

namespace SPO2SQL.SharePoint;

public class Context : IDisposable
{
    internal Web? web;

    public string Site { get; set; } = string.Empty;

    public SPOUser SPOUser { get; set; } = null!;

    public ClientContext Ctx { get; set; } = null!;

    public async Task BuildContextAsync(CancellationToken cancellationToken = default)
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
            Web currentWeb = Ctx.Web;
            web = currentWeb;

            Ctx.Load(currentWeb, w => w.Title, w => w.Url);
            await ExecuteQueryAsync(cancellationToken).ConfigureAwait(false);

            Logger.LogDebug($"SharePoint context established successfully for site: {currentWeb.Title}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to build SharePoint context for site '{Site}'", ex);
            throw;
        }
    }

    protected async Task ExecuteQueryAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Ctx.ExecuteQueryWithCancellationAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        Ctx?.Dispose();
        GC.SuppressFinalize(this);
    }

    public async Task<ListCollection> GetAllListsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            string baseUrl = ConfigurationReader.GetSharePointBaseUrl()
                ?? "https://bringglobal.sharepoint.com";
            string expectedUrl = $"{baseUrl.TrimEnd('/')}/{Site?.TrimStart('/') ?? string.Empty}";

            if (web == null || Ctx?.Url != expectedUrl)
            {
                Logger.LogDebug("Rebuilding context for GetAllLists");
                await BuildContextAsync(cancellationToken).ConfigureAwait(false);
            }

            var currentWeb = web ?? throw new InvalidOperationException("SharePoint context is not initialized.");
            var lists = currentWeb.Lists;
            Ctx!.Load(lists);
            await ExecuteQueryAsync(cancellationToken).ConfigureAwait(false);

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
