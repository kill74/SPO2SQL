using Microsoft.SharePoint.Client;
using System;
using System.Collections.Generic;

namespace Bring.Sharepoint
{
    /// <summary>
    /// Handles backfilling approver fields for 'invoice request' items
    /// based on recent Unit list changes.
    /// </summary>
    internal class InvoiceRequestDQ
    {
        /// <summary>
        /// Authenticated SharePoint user context for operations.
        /// </summary>
        public SPOUser Me { get; set; }

        /// <summary>
        /// Updates Invoice Request items modified in the last 3 days,
        /// setting approvers from the Unit list lookup.
        /// </summary>
        /// <returns>True when complete; exceptions may bubble out.</returns>
        public bool UpdateApprovers()
        {
            // Calculate cutoff date (3 days ago at midnight UTC)
            DateTime cutoff = DateTime.Today.AddDays(-3);
            string cutoffIso = $"{cutoff:yyyy-MM-dd}T00:00:00Z";

            // Build CAML to fetch Unit items modified since cutoff
            string unitQuery =
                "<View><Query><Where>"
              + $"<Geq><FieldRef Name='Modified' /><Value Type='DateTime'>{cutoffIso}</Value></Geq>"
              + "</Where></Query></View>";

            // Load recent Unit list items into a dictionary by Project ID
            var unitList = new SPOList
            {
                SPOUser = Me,
                Name = "Unit",
                Site = "seed",
                CAMLQuery = unitQuery
            };
            unitList.Build();

            var unitMap = new Dictionary<string, ListItem>();
            foreach (ListItem unit in unitList.ItemCollection)
            {
                // Use Project_x0020_ID as key
                string projectId = (string)unit["Project_x0020_ID"];
                unitMap[projectId] = unit;
            }

            // If no recent units, nothing to update
            if (unitMap.Count == 0) return true;

            // Build CAML for invoice requests whose Unit lookup matches our keys
            string invoiceQuery = QueryBuilder(unitMap);
            var invoiceList = new SPOList
            {
                SPOUser = Me,
                Name = "invoice request",
                Site = "selfservice/invoicerequest",
                CAMLQuery = invoiceQuery
            };
            invoiceList.Build();

            int batchCount = 0;
            // Iterate and copy approver fields from Unit to Invoice Request
            foreach (ListItem inv in invoiceList.ItemCollection)
            {
                // Lookup matching Unit item
                var lookup = (FieldLookupValue)inv["Unit_x002f_Project_x003a_Project0"];
                ListItem unitItem = unitMap[lookup.LookupValue];

                // Copy approvers
                inv["Main_x0020_approver"] = unitItem["Main_x0020_approver"];
                inv["Optional_x0020_approver"] = unitItem["Optional_x0020_approver"];
                inv["Financial_x0020_approver"] = unitItem["Financial_x0020_approver"];
                inv.Update();

                // Execute in batches of 80 to avoid throttling
                if (++batchCount % 80 == 0)
                {
                    invoiceList.Ctx.ExecuteQuery();
                }
            }

            // Execute any remaining updates
            Console.WriteLine("Executing last query");
            invoiceList.Ctx.ExecuteQuery();
            Console.WriteLine("Done executing last query");

            return true;
        }

        /// <summary>
        /// Pads single-digit numbers with leading zero.
        /// </summary>
        private string PadStr(int i)
        {
            return i < 10 ? "0" + i : i.ToString();
        }

        /// <summary>
        /// Constructs a CAML query string that ORs Eq conditions
        /// for each Unit key in the dictionary.
        /// </summary>
        private string QueryBuilder(Dictionary<string, ListItem> unitMap)
        {
            // Start the <Where> clause, prepending nested <Or> as needed
            string xml = OrAppend("<View><Query><Where>", unitMap.Count);
            bool first = true;
            foreach (var key in unitMap.Keys)
            {
                // Add an Eq statement for each Project key
                xml += $"<Eq><FieldRef Name='Unit_x002f_Project_x003a_Project0' />"
                     + $"<Value Type='Text'>{key}</Value></Eq>";
                if (!first) xml += "</Or>";
                first = false;
            }
            xml += "</Where></Query></View>";
            return xml;
        }

        /// <summary>
        /// Recursively appends <Or> tags for a CAML query
        /// based on the number of clauses required.
        /// </summary>
        private string OrAppend(string xml, int count)
        {
            if (count <= 1)
                return xml;
            // Nest one <Or> and recurse for remaining count - 1
            return OrAppend(xml + "<Or>", count - 1);
        }
    }
}
