using Microsoft.SharePoint.Client;
using System;
using System.Collections.Generic;
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
        /// Collection of items from the SharePoint list
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
        /// Build the list by loading from SharePoint
        /// </summary>
        public void Build()
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

                // Create CAML query
                CamlQuery camlQuery = string.IsNullOrEmpty(CAMLQuery)
                    ? CamlQuery.CreateAllItemsQuery()
                    : new CamlQuery { ViewXml = CAMLQuery };

                // Load list items
                _list = web.Lists.GetByTitle(Name);
                ItemCollection = _list.GetItems(camlQuery);
                Fields = _list.Fields;

                // Prepare context for execution
                Ctx.Load(_list);
                Ctx.Load(ItemCollection);
                Ctx.Load(Fields);

                Logger.Log(2, "Executing query to retrieve list items");
                Ctx.ExecuteQuery();

                Logger.LogDebug($"Successfully loaded {ItemCollection.Count} items from list '{Name}'");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to build list '{Name}'", ex);
                throw;
            }
        }

        /// <summary>
        /// Refresh list data by re-executing query
        /// </summary>
        public void Update()
        {
            try
            {
                Logger.Log(2, $"Updating list '{Name}'");
                Ctx.ExecuteQuery();
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

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Field|InternalName|Value|CanBeDeleted|Hidden|FieldType|ReadOnly|FromBaseType|Required|ItemValueType");

                foreach (Field field in (IEnumerable<Field>)Fields)
                {
                    try
                    {
                        object fieldValue = item[field.InternalName];
                        string formattedValue = FormatFieldOutput(field, fieldValue);
                        sb.AppendLine($"{field.Title}|{field.InternalName}|{formattedValue}|{field.CanBeDeleted}|{field.Hidden}|{field.TypeAsString}|{field.ReadOnlyField}|{field.FromBaseType}|{field.Required}|{fieldValue?.GetType().Name ?? "NULL"}");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"Could not retrieve value for field '{field.Title}': {ex.Message}");
                        sb.AppendLine($"{field.Title}|{field.InternalName}|ERROR|{field.CanBeDeleted}|{field.Hidden}|{field.TypeAsString}|{field.ReadOnlyField}|{field.FromBaseType}|{field.Required}|ERROR");
                    }
                }

                Logger.Log(1, sb.ToString());
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
