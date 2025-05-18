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

            try
            {
                // Retrieve SharePoint credentials from the configuration file
                var (username, password) = ConfigurationReader.GetSharePointCredentials();
                // Create a SharePoint user object with the retrieved credentials
                Console.WriteLine($"SPOtoSQLUpdate: Username: {username} Password: {password}");
                SPOUser user;
                try
                {
                    user = new SPOUser(username, password);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("SPOtoSQLUpdate: ERROR - Failed to create SPOUser.");
                    Console.WriteLine("Exception: " + ex.Message);
                    Console.WriteLine("Stack Trace: " + ex.StackTrace);
                    return;
                }

                // Iterate through all keys in the AppSettings section of the configuration file
                foreach (string allKey in ConfigurationManager.AppSettings.AllKeys)
                {
                    string listName = allKey;
                    string ctxURL = ConfigurationManager.AppSettings[allKey];
                    Console.WriteLine($"SPOtoSQLUpdate: Processing list: {listName} with URL: {ctxURL}");
                    try
                    {
                        RefreshSQLLists.RefreshListsSQL(listName, ctxURL, user, daily);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"SPOtoSQLUpdate: ERROR - Exception while updating list '{listName}'.");
                        Console.WriteLine("Exception: " + ex.Message);
                        Console.WriteLine("Stack Trace: " + ex.StackTrace);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("SPOtoSQLUpdate: FATAL ERROR - Exception during SPO to SQL update process.");
                Console.WriteLine("Exception: " + ex.Message);
                Console.WriteLine("Stack Trace: " + ex.StackTrace);
            }

            Console.WriteLine("SPOtoSQLUpdate: SPO to SQL update completed.");
        }

        // Method to handle the update process for a specific SharePoint list to SQL Server
        public static void RefreshListsSQL(string listName, string ctxURL, SPOUser user, bool daily)
        {
            try
            {
                Console.WriteLine($"RefreshListsSQL: Initializing SPO list for: {listName}");

                SPOList spoList = null;
                try
                {
                    spoList = new SPOList();
                    spoList.Site = ctxURL;
                    spoList.SPOUser = user;
                    spoList.Name = listName;

                    Console.WriteLine("RefreshListsSQL: SPOList initialized:");
                    Console.WriteLine($"  Site: {spoList.Site}");
                    Console.WriteLine($"  Name: {spoList.Name}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"RefreshListsSQL: ERROR - Failed to initialize SPOList for '{listName}'.");
                    Console.WriteLine("Exception: " + ex.Message);
                    Console.WriteLine("Stack Trace: " + ex.StackTrace);
                    return;
                }

                SQLInteraction sqlInteraction = null;
                try
                {
                    sqlInteraction = new SQLInteraction()
                    {
                        List = spoList
                    };

                    Console.WriteLine("RefreshListsSQL: Building SQL interaction...");
                    sqlInteraction.Build();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"RefreshListsSQL: ERROR - Failed to build SQLInteraction for '{listName}'.");
                    Console.WriteLine("Exception: " + ex.Message);
                    Console.WriteLine("Stack Trace: " + ex.StackTrace);
                    return;
                }

                try
                {
                    if (daily)
                    {
                        Console.WriteLine("RefreshListsSQL: Performing daily update...");
                        sqlInteraction.DailyUpdate();
                    }
                    else
                    {
                        Console.WriteLine("RefreshListsSQL: Performing current time update...");
                        sqlInteraction.CurrentTimeUpdate();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"RefreshListsSQL: ERROR - Exception during update for '{listName}'.");
                    Console.WriteLine("Exception: " + ex.Message);
                    Console.WriteLine("Stack Trace: " + ex.StackTrace);
                    return;
                }

                Console.WriteLine($"RefreshListsSQL: Update for list '{listName}' completed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RefreshListsSQL: FATAL ERROR updating list '{listName}': {ex.Message}");
                Console.WriteLine("Stack Trace: " + ex.StackTrace);
            }
        }
    }
}