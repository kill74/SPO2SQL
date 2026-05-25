using System;
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

    public override bool Execute()
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

            var timesheetList = CreateAndBuildList(TIMESHEET_LIST, TIMESHEET_SITE, camlQuery);

            if (timesheetList.ItemCollection.Count == 0)
            {
                Logger.LogWarning("No timesheet items found with missing Main approver");
                return true;
            }

            ProcessListItemsInBatches(timesheetList, item =>
            {
                try
                {
                    var unitRecord = GetUnitItem(item);
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
                        var structureApprover = GetStructureApprover(item);
                        if (structureApprover != null)
                        {
                            SetFieldValue(item, MAIN_APPROVER_FIELD, structureApprover);
                        }
                    }

                    item.Update();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error processing Timesheet item ID {item.Id}: {ex.Message}");
                }
            });

            Logger.LogWarning("Completed Timesheet approver backfill operation");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to update timesheet data quality", ex);
            return false;
        }
    }

    private ListItem GetUnitItem(ListItem item)
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
                $"<Eq><FieldRef Name='Project_x0020_ID' /><Value Type='Text'>{projectLookup.LookupValue}</Value></Eq>" +
                "<Eq><FieldRef Name='Active' /><Value Type='Text'>Yes</Value></Eq>" +
                "</And></Where></Query>" +
                "</View>";

            var unitList = CreateAndBuildList(UNIT_LIST, UNIT_SITE, camlUnitQuery);
            return unitList.ItemCollection.Count > 0 ? unitList.ItemCollection[0] : null;
        }
        catch (Exception ex)
        {
            Logger.LogDebug($"Could not retrieve Unit item: {ex.Message}");
            return null;
        }
    }

    private FieldUserValue GetStructureApprover(ListItem item)
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

            var hrList = CreateAndBuildList(HR_LIST, HR_SITE, camlPersonQuery);
            if (hrList.ItemCollection.Count > 0)
            {
                return GetFieldValue<FieldUserValue>(hrList.ItemCollection[0], "Approver1");
            }

            return null;
        }
        catch (Exception ex)
        {
            Logger.LogDebug($"Could not retrieve structure approver: {ex.Message}");
            return null;
        }
    }
}
