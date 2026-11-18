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

    public override async Task<bool> ExecuteAsync(CancellationToken cancellationToken = default)
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

            using var unitList = await CreateAndBuildListAsync(UNIT_LIST, UNIT_SITE, unitQuery, cancellationToken)
                .ConfigureAwait(false);

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

            string invoiceQuery = BuildInvoiceQuery(unitMap);
            using var invoiceList = await CreateAndBuildListAsync(INVOICE_LIST, INVOICE_SITE, invoiceQuery, cancellationToken)
                .ConfigureAwait(false);

            if (invoiceList.ItemCollection.Count == 0)
            {
                Logger.LogWarning("No Invoice Request items found to update");
                return true;
            }

            await ProcessListItemsInBatchesAsync(invoiceList, item =>
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

                return Task.CompletedTask;
            }, cancellationToken: cancellationToken).ConfigureAwait(false);

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

        var conditions = unitMap.Keys.Select(key =>
            $"<Eq><FieldRef Name='{UNIT_LOOKUP_FIELD}' />" +
            $"<Value Type='Text'>{CamlQueryBuilder.EscapeXmlValue(key)}</Value></Eq>").ToArray();

        return conditions.Length == 1
            ? $"<View><Query><Where>{conditions[0]}</Where></Query></View>"
            : CamlQueryBuilder.BuildOrQuery(conditions);
    }
}
