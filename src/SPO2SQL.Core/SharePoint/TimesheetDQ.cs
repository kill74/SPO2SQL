using Microsoft.SharePoint.Client;
using SPO2SQL.Logging;

namespace SPO2SQL.SharePoint;

internal sealed class TimesheetDQ : DataQualityBase
{
    private const string TIMESHEET_LIST = "Timesheet";
    private const string TIMESHEET_SITE = "selfservice/timesheet";
    private const string UNIT_LIST = "Unit";
    private const string UNIT_SITE = "seed";
    private const string HR_LIST = "HR Database";
    private const string HR_SITE = "people";
    private const string MAIN_APPROVER_FIELD = "Main_x0020_approver";
    private const string OPTIONAL_APPROVER_FIELD = "Optional_x0020_approver";

    public TimesheetDQ(SPOUser user) : base(user)
    {
    }

    public override async Task<bool> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogWarning("Starting Timesheet approver backfill operation");

            string camlQuery =
                "<View>" +
                "<Query><Where><IsNull>" +
                $"<FieldRef Name='{MAIN_APPROVER_FIELD}' />" +
                "</IsNull></Where></Query>" +
                "</View>";

            using var timesheetList = await CreateAndBuildListAsync(TIMESHEET_LIST, TIMESHEET_SITE, camlQuery, cancellationToken)
                .ConfigureAwait(false);

            if (timesheetList.ItemCollection.Count == 0)
            {
                Logger.LogWarning("No timesheet items found with missing Main approver");
                return true;
            }

            await ProcessListItemsInBatchesAsync(timesheetList, async item =>
            {
                try
                {
                    var unitRecord = await GetUnitItemAsync(item, cancellationToken).ConfigureAwait(false);
                    if (unitRecord != null && unitRecord[MAIN_APPROVER_FIELD] != null)
                    {
                        SetFieldValue(item, MAIN_APPROVER_FIELD, unitRecord[MAIN_APPROVER_FIELD]);
                        var optionalApprover = GetFieldValue<object>(unitRecord, OPTIONAL_APPROVER_FIELD);
                        if (optionalApprover != null)
                        {
                            SetFieldValue(item, OPTIONAL_APPROVER_FIELD, optionalApprover);
                        }
                    }
                    else
                    {
                        var structureApprover = await GetStructureApproverAsync(item, cancellationToken).ConfigureAwait(false);
                        if (structureApprover != null)
                        {
                            SetFieldValue(item, MAIN_APPROVER_FIELD, structureApprover);
                        }
                    }

                    item.Update();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error processing Timesheet item ID {item.Id}", ex);
                }
            }, cancellationToken: cancellationToken).ConfigureAwait(false);

            Logger.LogWarning("Completed Timesheet approver backfill operation");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to update timesheet data quality", ex);
            return false;
        }
    }

    private async Task<ListItem?> GetUnitItemAsync(ListItem item, CancellationToken cancellationToken)
    {
        try
        {
            var projectLookup = GetFieldValue<FieldLookupValue>(item, "Unit_x003a_Project_x0020_ID");
            if (projectLookup == null)
            {
                return null;
            }

            string camlUnitQuery =
                "<View>" +
                "<Query><Where><And>" +
                $"<Eq><FieldRef Name='Project_x0020_ID' /><Value Type='Text'>{CamlQueryBuilder.EscapeXmlValue(projectLookup.LookupValue)}</Value></Eq>" +
                "<Eq><FieldRef Name='Active' /><Value Type='Text'>Yes</Value></Eq>" +
                "</And></Where></Query>" +
                "</View>";

            using var unitList = await CreateAndBuildListAsync(UNIT_LIST, UNIT_SITE, camlUnitQuery, cancellationToken)
                .ConfigureAwait(false);
            return unitList.ItemCollection.Count > 0 ? unitList.ItemCollection[0] : null;
        }
        catch (Exception ex)
        {
            Logger.LogDebug($"Could not retrieve Unit item: {ex.Message}");
            return null;
        }
    }

    private async Task<FieldUserValue?> GetStructureApproverAsync(ListItem item, CancellationToken cancellationToken)
    {
        try
        {
            var resourceLookup = GetFieldValue<FieldLookupValue>(item, "Resource");
            if (resourceLookup == null)
            {
                return null;
            }

            string camlPersonQuery =
                "<View>" +
                "<Query><Where>" +
                $"<Eq><FieldRef Name='Display_x0020_Name' LookupId='TRUE'/>" +
                $"<Value Type='Integer'>{resourceLookup.LookupId}</Value></Eq>" +
                "</Where></Query>" +
                "</View>";

            using var hrList = await CreateAndBuildListAsync(HR_LIST, HR_SITE, camlPersonQuery, cancellationToken)
                .ConfigureAwait(false);
            return hrList.ItemCollection.Count > 0
                ? GetFieldValue<FieldUserValue>(hrList.ItemCollection[0], "Approver1")
                : null;
        }
        catch (Exception ex)
        {
            Logger.LogDebug($"Could not retrieve structure approver: {ex.Message}");
            return null;
        }
    }
}
