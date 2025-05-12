using Bring.Sharepoint;
using Bring.XmlConfig;
using System;
using System.Configuration;

namespace Bring.Sqlserver
{
    internal class RefreshSQLLists
    {
        // Method to initiate the update process from SharePoint to SQL Server
        public static void SPOtoSQLUpdate(bool daily)
        {
            Console.WriteLine("SPOtoSQLUpdate: Starting SPO to SQL update. Daily: " + daily);

            // Retrieve SharePoint credentials from the configuration file
            var (username, password) = ConfigurationReader.GetSharePointCredentials();
            // Create a SharePoint user object with the retrieved credentials
            Console.WriteLine($"SPOtoSQLUpdate: {username} {password}");
            SPOUser user = new SPOUser(username, password);

            // Iterate through all keys in the AppSettings section of the configuration file
            // Each key represents a SharePoint list name, and its value is the site URL
            foreach (string allKey in ConfigurationManager.AppSettings.AllKeys)
            {
                string listName = allKey; // The key is the SharePoint list name
                string ctxURL = ConfigurationManager.AppSettings[allKey]; // The value is the SharePoint site URL
                Console.WriteLine($"SPOtoSQLUpdate: Processing list: {listName} with URL: {ctxURL}");
                // Call the method to refresh the SQL data for this specific list
                RefreshSQLLists.RefreshListsSQL(listName, ctxURL, user, daily);
            }

            Console.WriteLine("SPOtoSQLUpdate: SPO to SQL update completed.");
        }

        // Method to handle the update process for a specific SharePoint list to SQL Server
        public static void RefreshListsSQL(string listName, string ctxURL, SPOUser user, bool daily)
        {
            try
            {
                Console.WriteLine($"RefreshListsSQL: Initializing SPO list for: {listName}");   

                // Create a new SPOList object to represent the SharePoint list
                SPOList spoList = new SPOList();
                spoList.Site = ctxURL; // Set the SharePoint site URL
                spoList.SPOUser = user; // Assign the SharePoint user credentials
                spoList.Name = listName; // Set the name of the SharePoint list

                Console.WriteLine("RefreshListsSQL: SPOList initialized:");
                Console.WriteLine($"  Site: {spoList.Site}");
                Console.WriteLine($"  Name: {spoList.Name}");

                // Create an SQLInteraction object to manage SQL operations for this list
                SQLInteraction sqlInteraction = new SQLInteraction()
                {
                    List = spoList // Assign the SharePoint list to the SQLInteraction object
                };

                Console.WriteLine("RefreshListsSQL: Building SQL interaction...");
                // Build the SQL interaction, likely setting up the SQL connection and table structure
                sqlInteraction.Build();

                // Perform the appropriate update based on the 'daily' flag
                if (daily)
                {
                    Console.WriteLine("RefreshListsSQL: Performing daily update...");
                    // Execute a daily update, possibly deleting and re-inserting all data
                    sqlInteraction.DailyUpdate();
                }
                else
                {
                    Console.WriteLine("RefreshListsSQL: Performing current time update...");
                    // Execute an update based on the current timestamp
                    sqlInteraction.CurrentTimeUpdate();
                }

                Console.WriteLine($"RefreshListsSQL: Update for list '{listName}' completed.");
            }
            catch (Exception ex)
            {
                // Handle and log any exceptions that occur during the update process
                Console.WriteLine($"RefreshListsSQL: Error updating list '{listName}': {ex.Message}");
            }
        }
    }
}