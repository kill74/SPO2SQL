using Microsoft.SharePoint.Client;
using System;
using Bring.SPODataQuality;

namespace Bring.Sharepoint
{
    /// <summary>
    /// Base class for data quality operations on SharePoint lists.
    /// Provides common functionality for initializing lists, processing items, and executing batch updates.
    /// </summary>
    internal abstract class DataQualityBase
    {
        /// <summary>
        /// The authenticated SharePoint user context for all operations.
        /// </summary>
        protected SPOUser User { get; set; }

        /// <summary>
        /// Initializes the data quality base with a SharePoint user context.
        /// </summary>
        /// <param name="user">The authenticated SPOUser for SharePoint operations.</param>
        protected DataQualityBase(SPOUser user)
        {
            User = user ?? throw new ArgumentNullException(nameof(user));
        }

        /// <summary>
        /// Executes the data quality update operation.
        /// Must be implemented by derived classes.
        /// </summary>
        /// <returns>True if the operation completed successfully; false otherwise.</returns>
        public abstract bool Execute();

        /// <summary>
        /// Helper method to create a SharePoint list with standard initialization.
        /// </summary>
        /// <param name="listName">The name of the SharePoint list.</param>
        /// <param name="site">The relative site path (e.g., "selfservice/timesheet").</param>
        /// <param name="camlQuery">Optional CAML query to filter items.</param>
        /// <returns>An initialized SPOList object.</returns>
        protected SPOList CreateAndBuildList(string listName, string site, string camlQuery = null)
        {
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

        /// <summary>
        /// Processes each item in a list collection and applies updates in batches.
        /// </summary>
        /// <param name="list">The SPOList to process.</param>
        /// <param name="processor">Delegate to process each item.</param>
        /// <param name="batchSize">Number of items to process before calling ExecuteQuery. Default is 80.</param>
        protected void ProcessListItemsInBatches(SPOList list, Action<ListItem> processor, int batchSize = 80)
        {
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

                // Execute any remaining updates
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

        /// <summary>
        /// Safely extracts a field value from a list item, handling null and type casting.
        /// </summary>
        /// <typeparam name="T">The expected type of the field value.</typeparam>
        /// <param name="item">The list item.</param>
        /// <param name="fieldName">The internal field name.</param>
        /// <returns>The field value, or default if null or not found.</returns>
        protected T GetFieldValue<T>(ListItem item, string fieldName) where T : class
        {
            try
            {
                if (item == null) return null;
                if (item[fieldName] is T value) return value;
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"Failed to retrieve field '{fieldName}' from item: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Safely sets a field value on a list item.
        /// </summary>
        /// <param name="item">The list item to update.</param>
        /// <param name="fieldName">The internal field name.</param>
        /// <param name="value">The value to set.</param>
        protected void SetFieldValue(ListItem item, string fieldName, object value)
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
}
