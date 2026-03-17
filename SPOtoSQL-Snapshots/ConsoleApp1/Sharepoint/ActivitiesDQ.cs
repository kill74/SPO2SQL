using Microsoft.SharePoint.Client;
using System;
using Bring.SPODataQuality;

namespace Bring.Sharepoint
{
    /// <summary>
    /// Handles backfilling the '_OpportunityID' field on 'activities' items
    /// when it is currently null, by copying the value from 'OpportunityID'.
    /// </summary>
    internal class ActivitiesDQ : DataQualityBase
    {
        private const string LIST_NAME = "activities";
        private const string SITE = "wolf";
        private const string TARGET_FIELD = "_OpportunityID";
        private const string SOURCE_FIELD = "OpportunityID";

        /// <summary>
        /// Initializes a new ActivitiesDQ instance with a SharePoint user context.
        /// </summary>
        /// <param name="user">The authenticated SharePoint user.</param>
        public ActivitiesDQ(SPOUser user) : base(user)
        {
        }

        /// <summary>
        /// Executes the data quality operation.
        /// Queries the 'activities' list for items where '_OpportunityID' is null,
        /// then updates each item's '_OpportunityID' field to match 'OpportunityID'.
        /// </summary>
        /// <returns>True if processing completed successfully.</returns>
        public override bool Execute()
        {
            try
            {
                Logger.LogWarning($"Starting {LIST_NAME} update: backfilling {TARGET_FIELD} from {SOURCE_FIELD}");

                // Define a CAML query to find items lacking the custom _OpportunityID field
                string camlQuery =
                    "<View>" +
                    "<Query><Where><IsNull>" +
                    $"<FieldRef Name='{TARGET_FIELD}' />" +
                    "</IsNull></Where></Query>" +
                    "</View>";

                // Create and build the activities list
                var activitiesList = CreateAndBuildList(LIST_NAME, SITE, camlQuery);

                if (activitiesList.ItemCollection.Count == 0)
                {
                    Logger.LogWarning($"No items found with null {TARGET_FIELD} in {LIST_NAME}");
                    return true;
                }

                // Process each item in batches
                ProcessListItemsInBatches(activitiesList, item =>
                {
                    var sourceValue = GetFieldValue<object>(item, SOURCE_FIELD);
                    if (sourceValue != null)
                    {
                        SetFieldValue(item, TARGET_FIELD, sourceValue);
                        item.Update();
                    }
                });

                Logger.LogWarning($"Completed {LIST_NAME} update: {activitiesList.ItemCollection.Count} items processed");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to update activities data quality", ex);
                return false;
            }
        }
    }
}
