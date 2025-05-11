using Microsoft.SharePoint.Client;
using System;

namespace Bring.Sharepoint
{
    /// <summary>
    /// Handles backfilling approver fields in the 'Timesheet' list when missing.
    /// Queries related lists to determine the correct approvers based on unit or structure.
    /// </summary>
    internal class TimesheetDQ
    {
        /// <summary>
        /// The authenticated SharePoint user context used for list operations.
        /// </summary>
        public SPOUser Me { get; set; }

        /// <summary>
        /// Updates each Timesheet item missing a Main approver by copying from Unit or structure logic.
        /// </summary>
        /// <returns>True if processing completes without unhandled exceptions.</returns>
        public bool UpdateApprovers()
        {
            // CAML to select timesheet items where Main_approver is null
            string camlQuery =
                "<View>"
              + "<Query><Where><IsNull>"
              + "<FieldRef Name='Main_x0020_approver' />"
              + "</IsNull></Where></Query>"
              + "</View>";

            // Initialize the Timesheet list wrapper
            var timesheetList = new SPOList
            {
                Name = "Timesheet",
                Site = "selfservice/timesheet",
                SPOUser = this.Me,
                CAMLQuery = camlQuery
            };

            // Retrieve matching items
            timesheetList.Build();

            // Iterate through each item to assign approvers
            foreach (ListItem item in timesheetList.ItemCollection)
            {
                try
                {
                    // Try retrieving the unit-specific approver record
                    var unitRecord = GetUnitItem(item);
                    if (unitRecord != null && unitRecord["Main_x0020_approver"] != null)
                    {
                        // Copy both main and optional approvers from Unit list
                        item["Main_x0020_approver"] = unitRecord["Main_x0020_approver"];
                        item["Optional_x0020_approver"] = unitRecord["Optional_x0020_approver"];
                    }
                    else
                    {
                        // Fall back to organization structure approver
                        item["Main_x0020_approver"] = GetStructureApprover(item);
                    }

                    item.Update(); // Queue update for this item
                }
                catch (Exception ex)
                {
                    // Log any exceptions but continue processing
                    Console.WriteLine($"Error processing item ID {item.Id}: {ex.Message}");
                }
            }

            // Execute all queued updates in one batch
            timesheetList.Ctx.ExecuteQuery();
            return true;
        }

        /// <summary>
        /// Retrieves a Unit list record corresponding to the item's Unit:Project ID, if active.
        /// </summary>
        /// <param name="item">The timesheet ListItem being processed.</param>
        /// <returns>The first matching Unit ListItem or null if none found.</returns>
        private ListItem GetUnitItem(ListItem item)
        {
            // Build CAML to find active unit by project ID lookup value
            string projectId = ((FieldLookupValue)item["Unit_x003a_Project_x0020_ID"]).LookupValue;
            string camlUnitQuery =
                "<View>"
              + "<Query><Where><And>"
              + "<Eq><FieldRef Name='Project_x0020_ID' /><Value Type='Text'>" + projectId + "</Value></Eq>"
              + "<Eq><FieldRef Name='Active' /><Value Type='Text'>Yes</Value></Eq>"
              + "</And></Where></Query>"
              + "</View>";

            var unitList = new SPOList
            {
                Name = "Unit",
                Site = "seed",
                SPOUser = this.Me,
                CAMLQuery = camlUnitQuery
            };

            unitList.Build();
            return unitList.ItemCollection.Count > 0 ? unitList.ItemCollection[0] : null;
        }

        /// <summary>
        /// Looks up the approver based on the organizational structure for a given resource.
        /// </summary>
        /// <param name="item">The timesheet ListItem being processed.</param>
        /// <returns>A FieldUserValue representing the structure-based approver, or null.</returns>
        private FieldUserValue GetStructureApprover(ListItem item)
        {
            // Build CAML to find person by Resource lookup ID
            int resourceId = ((FieldLookupValue)item["Resource"]).LookupId;
            string camlPersonQuery =
                "<View>"
              + "<Query><Where>"
              + "<Eq><FieldRef Name='Display_x0020_Name' LookupId='TRUE'/>"
              + "<Value Type='Integer'>" + resourceId + "</Value></Eq>"
              + "</Where></Query>"
              + "</View>";

            var hrList = new SPOList
            {
                Name = "HR Database",
                Site = "people",
                SPOUser = this.Me,
                CAMLQuery = camlPersonQuery
            };

            hrList.Build();
            if (hrList.ItemCollection.Count > 0)
            {
                // Return the primary approver field from HR record
                return (FieldUserValue)hrList.ItemCollection[0]["Approver1"];
            }

            return null; // No structure approver found
        }
    }
}
