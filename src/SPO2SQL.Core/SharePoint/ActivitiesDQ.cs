using System;
using Microsoft.SharePoint.Client;
using SPO2SQL.Logging;

namespace SPO2SQL.SharePoint;

internal sealed class ActivitiesDQ : DataQualityBase
{
    private const string LIST_NAME = "activities";
    private const string SITE = "wolf";
    private const string TARGET_FIELD = "_OpportunityID";
    private const string SOURCE_FIELD = "OpportunityID";

    public ActivitiesDQ(SPOUser user) : base(user)
    {
    }

    public override bool Execute()
    {
        try
        {
            Logger.LogWarning($"Starting {LIST_NAME} update: backfilling {TARGET_FIELD} from {SOURCE_FIELD}");

            string camlQuery =
                "<View>" +
                "<Query><Where><IsNull>" +
                $"<FieldRef Name='{TARGET_FIELD}' />" +
                "</IsNull></Where></Query>" +
                "</View>";

            var activitiesList = CreateAndBuildList(LIST_NAME, SITE, camlQuery);

            if (activitiesList.ItemCollection.Count == 0)
            {
                Logger.LogWarning($"No items found with null {TARGET_FIELD} in {LIST_NAME}");
                return true;
            }

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
