using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.SharePoint.Client;
using SPO2SQL.Logging;

namespace SPO2SQL.SharePoint;

internal sealed class InvoiceRequestDQ : DataQualityBase
{
    private const string INVOICE_LIST = "invoice request";
    private const string INVOICE_SITE = "selfservice/invoicerequest";
    private const string UNIT_LIST = "Unit";
    private const string UNIT_SITE = "seed";
    private const string MAIN_APPROVER_FIELD = "Main_x0020_approver";
    private const string OPTIONAL_APPROVER_FIELD = "Optional_x0020_approver";
    private const string FINANCIAL_APPROVER_FIELD = "Financial_x0020_approver";
    private const string UNIT_LOOKUP_FIELD = "Unit_x002f_Project_x003a_Project0";
    private const string PROJECT_ID_FIELD = "Project_x0020_ID";
    private const int LOOKBACK_DAYS = 3;

    public InvoiceRequestDQ(SPOUser user) : base(user)
    {
    }

    public override bool Execute()
    {
        try
        {
            Logger.LogWarning("Starting Invoice Request approver update operation");

            DateTime cutoff = DateTime.Today.AddDays(-LOOKBACK_DAYS);
            string cutoffIso = $"{cutoff:yyyy-MM-dd}T00:00:00Z";

            string unitQuery =
                "<View><Query><Where>" +
                $"<Geq><FieldRef Name='Modified' /><Value Type='DateTime'>{cutoffIso}</Value></Geq>" +
                "</Where></Query></View>";

            var unitList = CreateAndBuildList(UNIT_LIST, UNIT_SITE, unitQuery);

            var unitMap = new Dictionary<string, ListItem>();
            foreach (ListItem unit in unitList.ItemCollection)
            {
                var projectId = GetFieldValue<string>(unit, PROJECT_ID_FIELD);
                if (projectId != null)
                {
                    unitMap[projectId] = unit;
                }
            }

            if (unitMap.Count == 0)
            {
                Logger.LogWarning($"No Unit list items modified in the last {LOOKBACK_DAYS} days");
                return true;
            }

            Logger.LogDebug($"Found {unitMap.Count} recently modified Unit items");

            string invoiceQuery = BuildInvoiceQuery(unitMap);
            var invoiceList = CreateAndBuildList(INVOICE_LIST, INVOICE_SITE, invoiceQuery);

            if (invoiceList.ItemCollection.Count == 0)
            {
                Logger.LogWarning("No Invoice Request items found to update");
                return true;
            }

            Logger.LogDebug($"Found {invoiceList.ItemCollection.Count} Invoice Request items to process");

            ProcessListItemsInBatches(invoiceList, item =>
            {
                try
                {
                    var lookup = GetFieldValue<FieldLookupValue>(item, UNIT_LOOKUP_FIELD);
                    if (lookup != null && unitMap.TryGetValue(lookup.LookupValue, out var unitItem))
                    {
                        SetFieldValue(item, MAIN_APPROVER_FIELD, unitItem[MAIN_APPROVER_FIELD]);
                        SetFieldValue(item, OPTIONAL_APPROVER_FIELD, unitItem[OPTIONAL_APPROVER_FIELD]);
                        SetFieldValue(item, FINANCIAL_APPROVER_FIELD, unitItem[FINANCIAL_APPROVER_FIELD]);
                        item.Update();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogDebug($"Failed to update invoice item {item.Id}: {ex.Message}");
                }
            });

            Logger.LogWarning("Completed Invoice Request approver update operation");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to update invoice request data quality", ex);
            return false;
        }
    }

    private static string BuildInvoiceQuery(Dictionary<string, ListItem> unitMap)
    {
        if (unitMap.Count == 0)
        {
            return "<View><Query></Query></View>";
        }

        var xml = new StringBuilder("<View><Query><Where>");

        for (int i = 1; i < unitMap.Count; i++)
        {
            xml.Append("<Or>");
        }

        bool first = true;
        foreach (var key in unitMap.Keys)
        {
            xml.Append($"<Eq><FieldRef Name='{UNIT_LOOKUP_FIELD}' />" +
                       $"<Value Type='Text'>{EscapeXmlValue(key)}</Value></Eq>");

            if (!first)
            {
                xml.Append("</Or>");
            }

            first = false;
        }

        xml.Append("</Where></Query></View>");
        return xml.ToString();
    }

    private static string EscapeXmlValue(string value)
    {
        return value?.Replace("&", "&amp;")
                   ?.Replace("<", "&lt;")
                   ?.Replace(">", "&gt;")
                   ?.Replace("\"", "&quot;")
                   ?.Replace("'", "&apos;") ?? "";
    }
}
