using Microsoft.SharePoint.Client;
using System;
using System.Collections.Generic;
using Bring.SPODataQuality;

namespace Bring.Sharepoint
{
    /// <summary>
    /// Handles backfilling approver fields for 'invoice request' items
    /// based on recent Unit list changes.
    /// </summary>
    internal class InvoiceRequestDQ : DataQualityBase
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

        /// <summary>
        /// Initializes a new InvoiceRequestDQ instance with a SharePoint user context.
        /// </summary>
        /// <param name="user">The authenticated SharePoint user.</param>
        public InvoiceRequestDQ(SPOUser user) : base(user)
        {
        }

        /// <summary>
        /// Executes the data quality operation.
        /// Updates Invoice Request items modified in the last 3 days,
        /// setting approvers from the Unit list lookup.
        /// </summary>
        /// <returns>True if processing completed successfully.</returns>
        public override bool Execute()
        {
            try
            {
                Logger.LogWarning("Starting Invoice Request approver update operation");

                // Calculate cutoff date
                DateTime cutoff = DateTime.Today.AddDays(-LOOKBACK_DAYS);
                string cutoffIso = $"{cutoff:yyyy-MM-dd}T00:00:00Z";

                // Build CAML to fetch Unit items modified since cutoff
                string unitQuery =
                    "<View><Query><Where>" +
                    $"<Geq><FieldRef Name='Modified' /><Value Type='DateTime'>{cutoffIso}</Value></Geq>" +
                    "</Where></Query></View>";

                // Load recent Unit list items into a dictionary by Project ID
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

                // If no recent units, nothing to update
                if (unitMap.Count == 0)
                {
                    Logger.LogWarning($"No Unit list items modified in the last {LOOKBACK_DAYS} days");
                    return true;
                }

                Logger.LogDebug($"Found {unitMap.Count} recently modified Unit items");

                // Build CAML for invoice requests whose Unit lookup matches our keys
                string invoiceQuery = BuildInvoiceQuery(unitMap);
                var invoiceList = CreateAndBuildList(INVOICE_LIST, INVOICE_SITE, invoiceQuery);

                if (invoiceList.ItemCollection.Count == 0)
                {
                    Logger.LogWarning("No Invoice Request items found to update");
                    return true;
                }

                Logger.LogDebug($"Found {invoiceList.ItemCollection.Count} Invoice Request items to process");

                // Process and update approvers in batches
                ProcessListItemsInBatches(invoiceList, item =>
                {
                    try
                    {
                        var lookup = GetFieldValue<FieldLookupValue>(item, UNIT_LOOKUP_FIELD);
                        if (lookup != null && unitMap.TryGetValue(lookup.LookupValue, out var unitItem))
                        {
                            // Copy all approver fields from Unit to Invoice Request
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

        /// <summary>
        /// Constructs a CAML query that matches invoice requests to recently updated units.
        /// </summary>
        private string BuildInvoiceQuery(Dictionary<string, ListItem> unitMap)
        {
            if (unitMap.Count == 0)
                return "<View><Query></Query></View>";

            // Build nested <Or> clauses for all unit keys
            string xml = "<View><Query><Where>";
            
            // Add opening <Or> tags for all conditions except the first
            for (int i = 1; i < unitMap.Count; i++)
            {
                xml += "<Or>";
            }

            bool first = true;
            foreach (var key in unitMap.Keys)
            {
                xml += $"<Eq><FieldRef Name='{UNIT_LOOKUP_FIELD}' />" +
                       $"<Value Type='Text'>{EscapeXmlValue(key)}</Value></Eq>";
                
                if (!first) xml += "</Or>";
                first = false;
            }

            xml += "</Where></Query></View>";
            return xml;
        }

        /// <summary>
        /// Escapes special XML characters in field values.
        /// </summary>
        private string EscapeXmlValue(string value)
        {
            return value?.Replace("&", "&amp;")
                       ?.Replace("<", "&lt;")
                       ?.Replace(">", "&gt;")
                       ?.Replace("\"", "&quot;")
                       ?.Replace("'", "&apos;") ?? "";
        }
    }
}
