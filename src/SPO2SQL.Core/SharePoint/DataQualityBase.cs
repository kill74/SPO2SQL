using Microsoft.SharePoint.Client;
using SPO2SQL.Logging;

namespace SPO2SQL.SharePoint;

internal abstract class DataQualityBase
{
    protected SPOUser User { get; }

    protected DataQualityBase(SPOUser user)
    {
        User = user ?? throw new ArgumentNullException(nameof(user));
    }

    public abstract Task<bool> ExecuteAsync(CancellationToken cancellationToken = default);

    protected async Task<SPOList> CreateAndBuildListAsync(
        string listName,
        string site,
        string? camlQuery = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(listName);
        ArgumentException.ThrowIfNullOrWhiteSpace(site);

        Logger.LogDebug($"Creating list '{listName}' on site '{site}'");

        var list = new SPOList
        {
            Name = listName,
            Site = site,
            SPOUser = User,
            CAMLQuery = camlQuery
        };

        try
        {
            await list.BuildAsync(cancellationToken).ConfigureAwait(false);
            Logger.LogDebug($"List '{listName}' built successfully with {list.ItemCollection.Count} items");
            return list;
        }
        catch
        {
            list.Dispose();
            throw;
        }
    }

    protected static async Task ProcessListItemsInBatchesAsync(
        SPOList list,
        Func<ListItem, Task> processor,
        int batchSize = 80,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(processor);
        if (batchSize <= 0)
        {
            throw new ArgumentException("batchSize must be greater than 0", nameof(batchSize));
        }

        if (list.ItemCollection.Count == 0)
        {
            Logger.LogDebug($"No items to process in list '{list.Name}'");
            return;
        }

        int batchCount = 0;
        foreach (ListItem item in list.ItemCollection)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await processor(item).ConfigureAwait(false);

            if (++batchCount % batchSize == 0)
            {
                await list.Ctx.ExecuteQueryWithCancellationAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        if (batchCount % batchSize != 0)
        {
            await list.Ctx.ExecuteQueryWithCancellationAsync(cancellationToken).ConfigureAwait(false);
        }

        Logger.LogDebug($"Completed processing {batchCount} items from list '{list.Name}'");
    }

    protected static T? GetFieldValue<T>(ListItem item, string fieldName)
    {
        try
        {
            return item[fieldName] is T value ? value : default;
        }
        catch (Exception ex)
        {
            Logger.LogDebug($"Failed to retrieve field '{fieldName}' from item: {ex.Message}");
            return default;
        }
    }

    protected static void SetFieldValue(ListItem item, string fieldName, object? value) => item[fieldName] = value;
}
