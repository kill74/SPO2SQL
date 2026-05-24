using Microsoft.SharePoint.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using Bring.SPODataQuality;

namespace Bring.Sharepoint
{
    /// <summary>
    /// Wrapper for SharePoint Online list with item and field management capabilities
    /// </summary>
    public class SPOList : Context
    {
        private List _list;

        /// <summary>
        /// Name of the SharePoint list
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Collection of items from the SharePoint list (last page if paginated).
        /// Use <see cref="AllItems"/> for all items across all pages when pagination is enabled.
        /// </summary>
        public ListItemCollection ItemCollection { get; set; }

        /// <summary>
        /// Collection of fields in the SharePoint list
        /// </summary>
        public FieldCollection Fields { get; set; }

        /// <summary>
        /// CAML query to filter list items
        /// </summary>
        public string CAMLQuery { get; set; }

        /// <summary>
        /// All items accumulated across pagination pages, or null if not paginated.
        /// </summary>
        public List<ListItem> AllItems { get; private set; }

        /// <summary>
        /// Build the list by loading from SharePoint.
        /// </summary>
        public void Build(int? pageSize = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Name))
                {
                    throw new InvalidOperationException("List 'Name' property must be set before calling Build()");
                }

                Logger.Log(2, $"Building list '{Name}' from site '{Site}'");

                if (web == null)
                {
                    BuildContext();
                }

                _list = web.Lists.GetByTitle(Name);
                Fields = _list.Fields;
                Ctx.Load(_list);
                Ctx.Load(Fields);

                if (pageSize.HasValue && pageSize.Value > 0)
                {
                    BuildWithPagination(pageSize.Value);
                }
                else
                {
                    BuildSinglePage();
                }

                Logger.LogDebug($"Successfully loaded {ItemCollection?.Count ?? 0} items from list '{Name}'");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to build list '{Name}'", ex);
                throw;
            }
        }

        private void BuildSinglePage()
        {
            CamlQuery camlQuery = string.IsNullOrEmpty(CAMLQuery)
                ? CamlQuery.CreateAllItemsQuery()
                : new CamlQuery { ViewXml = CAMLQuery };

            ItemCollection = _list.GetItems(camlQuery);
            Ctx.Load(ItemCollection);
            Ctx.ExecuteQuery();
        }

        private void BuildWithPagination(int pageSize)
        {
            AllItems = new List<ListItem>();
            int pageNum = 0;
            CamlQuery camlQuery;

            do
            {
                string baseXml = string.IsNullOrEmpty(CAMLQuery)
                    ? "<View><Query></Query></View>"
                    : CAMLQuery;

                // Inject RowLimit at View level
                if (!baseXml.Contains("<RowLimit>"))
                {
                    baseXml = baseXml.TrimEnd();
                    if (baseXml.EndsWith("</View>"))
                        baseXml = baseXml.Substring(0, baseXml.Length - 7) + $"<RowLimit>{pageSize}</RowLimit></View>";
                }

                camlQuery = new CamlQuery { ViewXml = baseXml };

                if (pageNum > 0 && ItemCollection?.ListItemCollectionPosition != null)
                {
                    camlQuery.ListItemCollectionPosition = ItemCollection.ListItemCollectionPosition;
                }

                ItemCollection = _list.GetItems(camlQuery);
                Ctx.Load(ItemCollection);
                Ctx.ExecuteQuery();

                foreach (ListItem item in ItemCollection)
                    AllItems.Add(item);

                pageNum++;
            }
            while (ItemCollection?.ListItemCollectionPosition != null);
        }

        /// <summary>
        /// Refresh list data by re-executing query
        /// </summary>
        public void Update()
        {
            try
            {
                Logger.Log(2, $"Updating list '{Name}'");
                Build();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to update list '{Name}'", ex);
                throw;
            }
        }

        /// <summary>
        /// Add a new item to the list
        /// </summary>
        public ListItem AddItem()
        {
            try
            {
                if (_list == null)
                {
                    throw new InvalidOperationException($"List '{Name}' has not been built. Call Build() first.");
                }

                ListItem listItem = _list.AddItem(new ListItemCreationInformation());
                listItem.Update();
                Logger.LogDebug($"Added new item to list '{Name}'");
                return listItem;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to add item to list '{Name}'", ex);
                throw;
            }
        }

        /// <summary>
        /// Print all properties of a list item for debugging
        /// </summary>
        public void PrintItemProperties(ListItem item)
        {
            try
            {
                if (item == null)
                {
                    Logger.LogWarning("Cannot print properties of a null list item");
                    return;
                }

                if (Fields == null || Fields.Count == 0)
                {
                    Logger.LogWarning($"No fields available in list '{Name}' to print");
                    return;
                }

                Logger.Log(1, "Field|InternalName|Value|CanBeDeleted|Hidden|FieldType|ReadOnly|FromBaseType|Required|ItemValueType");

                foreach (Field field in (IEnumerable<Field>)Fields)
                {
                    try
                    {
                        object fieldValue = item[field.InternalName];
                        string formattedValue = FormatFieldOutput(field, fieldValue);
                        Logger.Log(1, $"{field.Title}|{field.InternalName}|{formattedValue}|{field.CanBeDeleted}|{field.Hidden}|{field.TypeAsString}|{field.ReadOnlyField}|{field.FromBaseType}|{field.Required}|{fieldValue?.GetType().Name ?? "NULL"}");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"Could not retrieve value for field '{field.Title}': {ex.Message}");
                        Logger.Log(1, $"{field.Title}|{field.InternalName}|ERROR|{field.CanBeDeleted}|{field.Hidden}|{field.TypeAsString}|{field.ReadOnlyField}|{field.FromBaseType}|{field.Required}|ERROR");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to print item properties", ex);
            }
        }

        /// <summary>
        /// Format field value for display
        /// </summary>
        private string FormatFieldOutput(Field field, object value)
        {
            if (value == null)
                return "NULL";

            try
            {
                if (value is FieldLookupValue lookupValue)
                    return lookupValue?.LookupValue ?? "NULL";

                if (value is FieldUserValue userValue)
                    return userValue?.LookupValue ?? "NULL";

                if (value is FieldLookupValue[] lookupArray)
                    return string.Join("; ", lookupArray?.Select(v => v?.LookupValue ?? "?") ?? new[] { "NULL" });

                if (value is FieldUserValue[] userArray)
                    return string.Join("; ", userArray?.Select(v => v?.LookupValue ?? "?") ?? new[] { "NULL" });

                return value?.ToString() ?? "NULL";
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"Error formatting field value: {ex.Message}");
                return "ERROR";
            }
        }
    }
}
