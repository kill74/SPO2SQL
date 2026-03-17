using Microsoft.SharePoint.Client;
using System;
using Bring.SPODataQuality;

namespace Bring.Sharepoint
{
    /// <summary>
    /// Handles backfilling approver fields in the 'Timesheet' list when missing.
    /// Queries related lists to determine the correct approvers based on unit or structure.
    /// </summary>
    internal class TimesheetDQ : DataQualityBase
    {
        private const string TIMESHEET_LIST = "Timesheet";
        private const string TIMESHEET_SITE = "selfservice/timesheet";
        private const string UNIT_LIST = "Unit";
        private const string UNIT_SITE = "seed";
        private const string HR_LIST = "HR Database";
        private const string HR_SITE = "people";
        private const string MAIN_APPROVER_FIELD = "Main_x0020_approver";
        private const string OPTIONAL_APPROVER_FIELD = "Optional_x0020_approver";

        /// <summary>
        /// Initializes a new TimesheetDQ instance with a SharePoint user context.
        /// </summary>
        /// <param name="user">The authenticated SharePoint user.</param>
        public TimesheetDQ(SPOUser user) : base(user)
        {
        }

        /// <summary>
        /// Executes the data quality operation.
        /// Updates each Timesheet item missing a Main approver by copying from Unit or structure logic.
        /// </summary>
        /// <returns>True if processing completed successfully.</returns>
        public override bool Execute()
        {
            try
            {
                Logger.LogWarning("Starting Timesheet approver backfill operation");

                // CAML to select timesheet items where Main_approver is null
                string camlQuery =
                    "<View>" +
                    "<Query><Where><IsNull>" +
                    $"<FieldRef Name='{MAIN_APPROVER_FIELD}' />" +
                    "</IsNull></Where></Query>" +
                    "</View>";

                // Create and build the Timesheet list
                var timesheetList = CreateAndBuildList(TIMESHEET_LIST, TIMESHEET_SITE, camlQuery);

                if (timesheetList.ItemCollection.Count == 0)
                {
                    Logger.LogWarning("No timesheet items found with missing Main approver");
                    return true;
                }

                // Process each item in batches
                ProcessListItemsInBatches(timesheetList, item =>
                {
                    try
                    {
                        // Try retrieving the unit-specific approver record
                        var unitRecord = GetUnitItem(item);
                        if (unitRecord != null && unitRecord[MAIN_APPROVER_FIELD] != null)
                        {
                            // Copy both main and optional approvers from Unit list
                            SetFieldValue(item, MAIN_APPROVER_FIELD, unitRecord[MAIN_APPROVER_FIELD]);
                            var optionalApprover = GetFieldValue<object>(unitRecord, OPTIONAL_APPROVER_FIELD);
                            if (optionalApprover != null)
                            {
                                SetFieldValue(item, OPTIONAL_APPROVER_FIELD, optionalApprover);
                            }
                        }
                        else
                        {
                            // Fall back to organization structure approver
                            var structureApprover = GetStructureApprover(item);
                            if (structureApprover != null)
                            {
                                SetFieldValue(item, MAIN_APPROVER_FIELD, structureApprover);
                            }
                        }

                        item.Update(); // Queue update for this item
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

        /// <summary>
        /// Retrieves a Unit list record corresponding to the item's Unit:Project ID, if active.
        /// </summary>
        /// <param name="item">The timesheet ListItem being processed.</param>
        /// <returns>The first matching Unit ListItem or null if none found.</returns>
        private ListItem GetUnitItem(ListItem item)
        {
            try
            {
                var projectLookup = GetFieldValue<FieldLookupValue>(item, "Unit_x003a_Project_x0020_ID");
                if (projectLookup == null) return null;

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

        /// <summary>
        /// Looks up the approver based on the organizational structure for a given resource.
        /// </summary>
        /// <param name="item">The timesheet ListItem being processed.</param>
        /// <returns>A FieldUserValue representing the structure-based approver, or null.</returns>
        private FieldUserValue GetStructureApprover(ListItem item)
        {
            try
            {
                var resourceLookup = GetFieldValue<FieldLookupValue>(item, "Resource");
                if (resourceLookup == null) return null;

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
}
