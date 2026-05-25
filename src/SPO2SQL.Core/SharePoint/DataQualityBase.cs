using System;
using Microsoft.SharePoint.Client;
using SPO2SQL.Logging;

namespace SPO2SQL.SharePoint;

internal abstract class DataQualityBase
{
    protected SPOUser User { get; set; }

    protected DataQualityBase(SPOUser user)
    {
        User = user ?? throw new ArgumentNullException(nameof(user));
    }

    public abstract bool Execute();

    protected SPOList CreateAndBuildList(string listName, string site, string camlQuery = null)
    {
        if (string.IsNullOrWhiteSpace(listName))
        {
            throw new ArgumentException("List name cannot be null or empty", nameof(listName));
        }

        if (string.IsNullOrWhiteSpace(site))
        {
            throw new ArgumentException("Site cannot be null or empty", nameof(site));
        }

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
            list.Build();
            Logger.LogDebug($"List '{listName}' built successfully with {list.ItemCollection?.Count ?? 0} items");
            return list;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to build list '{listName}' on site '{site}'", ex);
            throw;
        }
    }

    protected static void ProcessListItemsInBatches(SPOList list, Action<ListItem> processor, int batchSize = 80)
    {
        if (processor == null)
        {
            throw new ArgumentNullException(nameof(processor), "Item processor delegate cannot be null");
        }

        if (batchSize <= 0)
        {
            throw new ArgumentException("batchSize must be greater than 0", nameof(batchSize));
        }

        if (list?.ItemCollection == null || list.ItemCollection.Count == 0)
        {
            Logger.LogDebug($"No items to process in list '{list?.Name}'");
            return;
        }

        try
        {
            int batchCount = 0;
            foreach (ListItem item in list.ItemCollection)
            {
                processor(item);

                if (++batchCount % batchSize == 0)
                {
                    Logger.LogDebug($"Executing batch query after {batchCount} items");
                    list.Ctx.ExecuteQuery();
                }
            }

            if (batchCount % batchSize != 0)
            {
                Logger.LogDebug($"Executing final batch query for remaining {batchCount % batchSize} items");
                list.Ctx.ExecuteQuery();
            }

            Logger.LogDebug($"Completed processing {batchCount} items from list '{list.Name}'");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error processing items in list '{list.Name}'", ex);
            throw;
        }
    }

    protected static T GetFieldValue<T>(ListItem item, string fieldName)
    {
        try
        {
            if (item == null)
            {
                return default;
            }

            if (item[fieldName] is T value)
            {
                return value;
            }

            return default;
        }
        catch (Exception ex)
        {
            Logger.LogDebug($"Failed to retrieve field '{fieldName}' from item: {ex.Message}");
            return default;
        }
    }

    protected static void SetFieldValue(ListItem item, string fieldName, object value)
    {
        try
        {
            item[fieldName] = value;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to set field '{fieldName}' on item: {ex.Message}");
            throw;
        }
    }
}
