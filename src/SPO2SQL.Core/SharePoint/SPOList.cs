using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.SharePoint.Client;
using SPO2SQL.Logging;

namespace SPO2SQL.SharePoint;

public class SPOList : Context
{
    private static readonly string[] s_nullArray = ["NULL"];

    private List? _list;

    /// <summary>
    /// Number of SharePoint items requested per page. Bound to Sql:BatchSize.
    /// </summary>
    public int PageSize { get; set; } = 500;

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// All items returned by the query, across every SharePoint page.
    /// </summary>
    public IReadOnlyList<ListItem> ItemCollection { get; private set; } = Array.Empty<ListItem>();

    public FieldCollection Fields { get; set; } = null!;

    public string? CAMLQuery { get; set; }

    public async Task BuildAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                throw new InvalidOperationException("List 'Name' property must be set before calling BuildAsync()");
            }

            Logger.Log(2, $"Building list '{Name}' from site '{Site}'");

            if (web == null)
            {
                await BuildContextAsync(cancellationToken).ConfigureAwait(false);
            }

            CamlQuery camlQuery = string.IsNullOrEmpty(CAMLQuery)
                ? CamlQuery.CreateAllItemsQuery(PageSize)
                : new CamlQuery { ViewXml = CAMLQuery };

            _list = (web ?? throw new InvalidOperationException("SharePoint context is not initialized."))
                .Lists.GetByTitle(Name);
            Fields = _list.Fields;

            Ctx.Load(_list);
            Ctx.Load(Fields);
            await ExecuteQueryAsync(cancellationToken).ConfigureAwait(false);

            var items = new List<ListItem>();
            do
            {
                cancellationToken.ThrowIfCancellationRequested();

                ListItemCollection page = _list.GetItems(camlQuery);
                Ctx.Load(page);
                await ExecuteQueryAsync(cancellationToken).ConfigureAwait(false);

                items.AddRange(page);
                camlQuery.ListItemCollectionPosition = page.ListItemCollectionPosition;
            }
            while (camlQuery.ListItemCollectionPosition != null);

            ItemCollection = items;

            Logger.LogDebug($"Successfully loaded {ItemCollection.Count} items from list '{Name}'");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to build list '{Name}'", ex);
            throw;
        }
    }

    public ListItem GetItemById(int id) =>
        ItemCollection.FirstOrDefault(item => item.Id == id)
        ?? throw new InvalidOperationException($"Item with ID {id} was not loaded from list '{Name}'.");

    public async Task UpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.Log(2, $"Updating list '{Name}'");
            await ExecuteQueryAsync(cancellationToken).ConfigureAwait(false);
            await BuildAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to update list '{Name}'", ex);
            throw;
        }
    }

    public ListItem AddItem()
    {
        try
        {
            if (_list == null)
            {
                throw new InvalidOperationException($"List '{Name}' has not been built. Call BuildAsync() first.");
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
                    sb.AppendLine(CultureInfo.InvariantCulture, $"{field.Title}|{field.InternalName}|{formattedValue}|{field.CanBeDeleted}|{field.Hidden}|{field.TypeAsString}|{field.ReadOnlyField}|{field.FromBaseType}|{field.Required}|{fieldValue?.GetType().Name ?? "NULL"}");
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Could not retrieve value for field '{field.Title}': {ex.Message}");
                    sb.AppendLine(CultureInfo.InvariantCulture, $"{field.Title}|{field.InternalName}|ERROR|{field.CanBeDeleted}|{field.Hidden}|{field.TypeAsString}|{field.ReadOnlyField}|{field.FromBaseType}|{field.Required}|ERROR");
                }
            }

            Logger.Log(1, sb.ToString());
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to print item properties", ex);
        }
    }

    private static string FormatFieldOutput(Field field, object value)
    {
        if (value == null)
        {
            return "NULL";
        }

        try
        {
            if (value is FieldLookupValue lookupValue)
            {
                return lookupValue?.LookupValue ?? "NULL";
            }

            if (value is FieldUserValue userValue)
            {
                return userValue?.LookupValue ?? "NULL";
            }

            if (value is FieldLookupValue[] lookupArray)
            {
                return string.Join("; ", lookupArray?.Select(v => v?.LookupValue ?? "?") ?? s_nullArray);
            }

            if (value is FieldUserValue[] userArray)
            {
                return string.Join("; ", userArray?.Select(v => v?.LookupValue ?? "?") ?? s_nullArray);
            }

            return value?.ToString() ?? "NULL";
        }
        catch (Exception ex)
        {
            Logger.LogDebug($"Error formatting field value: {ex.Message}");
            return "ERROR";
        }
    }
}
