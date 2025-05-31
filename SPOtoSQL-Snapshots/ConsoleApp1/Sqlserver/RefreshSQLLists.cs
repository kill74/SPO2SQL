using Bring.Sharepoint;
using Bring.XmlConfig;
using System;
using System.Configuration;

namespace Bring.Sqlserver
{
    /// <summary>
    /// Provides functionality to synchronize SharePoint lists into SQL Server.
    /// </summary>
    internal class RefreshSQLLists
    {
        /// <summary>
        /// Initiates the update process from SharePoint to SQL Server for all configured lists.
        /// </summary>
        /// <param name="daily">Indicates whether to perform a daily incremental update or a full current-time refresh.</param>
        public static void SPOtoSQLUpdate(bool daily)
        {
            Console.WriteLine("SPOtoSQLUpdate: Starting SPO to SQL update. Daily: " + daily);

            try
            {
                // Load SharePoint credentials from configuration (secure storage recommended)
                var (username, password) = ConfigurationReader.GetSharePointCredentials();
                Console.WriteLine($"SPOtoSQLUpdate: Username: {username} Password: {password}");

                // Establish SharePoint user context
                SPOUser user;
                try
                {
                    user = new SPOUser(username, password);
                }
                catch (Exception ex)
                {
                    // Fail fast if authentication cannot be created
                    Console.WriteLine("SPOtoSQLUpdate: ERROR - Failed to create SPOUser.");
                    Console.WriteLine("Exception: " + ex.Message);
                    Console.WriteLine("Stack Trace: " + ex.StackTrace);
                    return;
                }

                // Iterate through each SharePoint list configured in AppSettings
                var listConfigs = ConfigurationReader.GetListConfigurations();
                if (listConfigs != null)
                {
                    foreach (var kvp in listConfigs)
                    {
                        var listName = kvp.Key;
                        var config = kvp.Value;
                        if (config.Ignore) continue; // Ignores the list if "ignore="true\""

                        string ctxURL = config.SharepointList;
                        Console.WriteLine($"SPOtoSQLUpdate: Processing list: {listName} with URL: {ctxURL}");
                        try
                        {
                            RefreshSQLLists.RefreshListsSQL(listName, ctxURL, user, daily);
                        }
                        catch (Exception ex)
                        {
                            // Log and continue on individual list errors
                            Console.WriteLine($"SPOtoSQLUpdate: ERROR - Exception while updating list '{listName}'.");
                            Console.WriteLine("Exception: " + ex.Message);
                            Console.WriteLine("Stack Trace: " + ex.StackTrace);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Catch-all for any unexpected failure in the orchestration
                Console.WriteLine("SPOtoSQLUpdate: FATAL ERROR - Exception during SPO to SQL update process.");
                Console.WriteLine("Exception: " + ex.Message);
                Console.WriteLine("Stack Trace: " + ex.StackTrace);
            }

            Console.WriteLine("SPOtoSQLUpdate: SPO to SQL update completed.");
        }

        /*
        /// <summary>
        /// Initiates the update process from SharePoint to SQL Server for all configured lists.
        /// </summary>
        /// <param name="daily">Indicates whether to perform a daily incremental update or a full current-time refresh.</param>
        public static void SPOtoSQLUpdateOLD(bool daily)
        {
            Console.WriteLine("SPOtoSQLUpdate: Starting SPO to SQL update. Daily: " + daily);

            try
            {
                // Load SharePoint credentials from configuration (secure storage recommended)
                var (username, password) = ConfigurationReader.GetSharePointCredentials();
                Console.WriteLine($"SPOtoSQLUpdate: Username: {username} Password: {password}");

                // Establish SharePoint user context
                SPOUser user;
                try
                {
                    user = new SPOUser(username, password);
                }
                catch (Exception ex)
                {
                    // Fail fast if authentication cannot be created
                    Console.WriteLine("SPOtoSQLUpdate: ERROR - Failed to create SPOUser.");
                    Console.WriteLine("Exception: " + ex.Message);
                    Console.WriteLine("Stack Trace: " + ex.StackTrace);
                    return;
                }

                // Iterate through each SharePoint list configured in AppSettings
                foreach (string allKey in ConfigurationManager.AppSettings.AllKeys)
                {
                    string listName = allKey;
                    string ctxURL = ConfigurationManager.AppSettings[allKey];
                    Console.WriteLine($"SPOtoSQLUpdate: Processing list: {listName} with URL: {ctxURL}");
                    try
                    {
                        // Delegate to RefreshListsSQL for per-list processing
                        RefreshSQLLists.RefreshListsSQL(listName, ctxURL, user, daily);
                    }
                    catch (Exception ex)
                    {
                        // Log and continue on individual list errors
                        Console.WriteLine($"SPOtoSQLUpdate: ERROR - Exception while updating list '{listName}'.");
                        Console.WriteLine("Exception: " + ex.Message);
                        Console.WriteLine("Stack Trace: " + ex.StackTrace);
                    }
                }
            }
            catch (Exception ex)
            {
                // Catch-all for any unexpected failure in the orchestration
                Console.WriteLine("SPOtoSQLUpdate: FATAL ERROR - Exception during SPO to SQL update process.");
                Console.WriteLine("Exception: " + ex.Message);
                Console.WriteLine("Stack Trace: " + ex.StackTrace);
            }

            Console.WriteLine("SPOtoSQLUpdate: SPO to SQL update completed.");
        }
        */

        /// <summary>
        /// Processes a specific SharePoint list: initializes context, builds SQL interaction, and performs the data transfer.
        /// </summary>
        /// <param name="listName">The name/key of the SharePoint list.</param>
        /// <param name="ctxURL">The site URL or context for the SharePoint list.</param>
        /// <param name="user">Authenticated SharePoint user context.</param>
        /// <param name="daily">Flag indicating whether a daily incremental or full refresh should be executed.</param>
        public static void RefreshListsSQL(string listName, string ctxURL, SPOUser user, bool daily)
        {
            try
            {
                Console.WriteLine($"RefreshListsSQL: Initializing SPO list for: {listName}");

                // Initialize SharePoint list object
                SPOList spoList = null;
                try
                {
                    spoList = new SPOList
                    {
                        Site = ctxURL,
                        SPOUser = user,
                        Name = listName
                    };

                    Console.WriteLine("RefreshListsSQL: SPOList initialized:");
                    Console.WriteLine($"  Site: {spoList.Site}");
                    Console.WriteLine($"  Name: {spoList.Name}");
                }
                catch (Exception ex)
                {
                    // Log initialization failure and abort this list's update
                    Console.WriteLine($"RefreshListsSQL: ERROR - Failed to initialize SPOList for '{listName}'.");
                    Console.WriteLine("Exception: " + ex.Message);
                    Console.WriteLine("Stack Trace: " + ex.StackTrace);
                    return;
                }

                // Prepare SQL interaction based on the SharePoint list schema
                SQLInteraction sqlInteraction = null;
                try
                {
                    sqlInteraction = new SQLInteraction
                    {
                        List = spoList
                    };

                    Console.WriteLine("RefreshListsSQL: Building SQL interaction...");
                    sqlInteraction.Build();
                }
                catch (Exception ex)
                {
                    // Log and abort if building SQL commands fails
                    Console.WriteLine($"RefreshListsSQL: ERROR - Failed to build SQLInteraction for '{listName}'.");
                    Console.WriteLine("Exception: " + ex.Message);
                    Console.WriteLine("Stack Trace: " + ex.StackTrace);
                    return;
                }

                // Perform the actual data transfer based on the mode (daily vs. full)
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
                    // Log any exceptions during the update process
                    Console.WriteLine($"RefreshListsSQL: ERROR - Exception during update for '{listName}'.");
                    Console.WriteLine("Exception: " + ex.Message);
                    Console.WriteLine("Stack Trace: " + ex.StackTrace);
                    return;
                }

                Console.WriteLine($"RefreshListsSQL: Update for list '{listName}' completed.");
            }
            catch (Exception ex)
            {
                // Catch-all for any unforeseen errors in the list-specific workflow
                Console.WriteLine($"RefreshListsSQL: FATAL ERROR updating list '{listName}': {ex.Message}");
                Console.WriteLine("Stack Trace: " + ex.StackTrace);
            }
        }
    }
}
