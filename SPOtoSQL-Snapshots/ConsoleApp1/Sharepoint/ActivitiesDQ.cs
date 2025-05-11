using Microsoft.SharePoint.Client;

namespace Bring.Sharepoint
{
    /// <summary>
    /// Handles backfilling the '_OpportunityID' field on 'activities' items
    /// when it is currently null, by copying the value from 'OpportunityID'.
    /// </summary>
    internal class ActivitiesDQ
    {
        /// <summary>
        /// The current SharePoint user context, used for list operations.
        /// </summary>
        public SPOUser Me { get; set; }

        /// <summary>
        /// Queries the 'activities' list for items where '_OpportunityID' is null,
        /// then updates each item's '_OpportunityID' field to match 'OpportunityID'.
        /// </summary>
        /// <returns>Always returns true if execution completes without exception.</returns>
        public bool UpdateIDs()
        {
            // Define a CAML query to find items lacking the custom _OpportunityID field
            string camlQuery =
                "<View>"
              + "<Query><Where><IsNull>"
              + "<FieldRef Name='_OpportunityID' />"
              + "</IsNull></Where></Query>"
              + "</View>";

            // Initialize the SPOList wrapper for the 'activities' list
            var activitiesList = new SPOList
            {
                Name = "activities",
                Site = "wolf",
                SPOUser = this.Me,
                CAMLQuery = camlQuery
            };

            // Retrieve matching items from SharePoint
            activitiesList.Build();

            // Iterate through each item, copying OpportunityID into _OpportunityID
            foreach (ListItem item in activitiesList.ItemCollection)
            {
                // Copy the existing 'OpportunityID' value into the '_OpportunityID' field
                item["_OpportunityID"] = item["OpportunityID"];
                item.Update();
            }

            // Commit all pending updates to SharePoint in a single batch
            activitiesList.Ctx.ExecuteQuery();

            return true;
        }
    }
}
