using Bring.Sharepoint;
using Bring.XmlConfig;
using System;
using System.Configuration;

namespace Bring.Sqlserver
{
    internal class RefreshSQLLists
    {
        public static void SPOtoSQLUpdate(bool daily)
        {
            Console.WriteLine("Starting SPO to SQL update. Daily: " + daily);

            var (username, password) = ConfigurationReader.GetSharePointCredentials();
            SPOUser user = new SPOUser(username, password);

            foreach (string allKey in ConfigurationManager.AppSettings.AllKeys)
            {
                string listName = allKey;
                string ctxURL = ConfigurationManager.AppSettings[allKey];
                Console.WriteLine($"Processing list: {listName} with URL: {ctxURL}");
                RefreshSQLLists.RefreshListsSQL(listName, ctxURL, user, daily);
            }

            Console.WriteLine("SPO to SQL update completed.");
        }

        public static void RefreshListsSQL(string listName, string ctxURL, SPOUser user, bool daily)
        {
            try
            {
                Console.WriteLine($"Initializing SPO list for: {listName}");

                SPOList spoList = new SPOList();
                spoList.Site = ctxURL;
                spoList.SPOUser = user;
                spoList.Name = listName;

                Console.WriteLine("SPOList initialized:");
                Console.WriteLine($"  Site: {spoList.Site}");
                Console.WriteLine($"  Name: {spoList.Name}");

                SQLInteraction sqlInteraction = new SQLInteraction()
                {
                    List = spoList
                };

                Console.WriteLine("Building SQL interaction...");
                sqlInteraction.Build();

                if (daily)
                {
                    Console.WriteLine("Performing daily update...");
                    sqlInteraction.DailyUpdate();
                }
                else
                {
                    Console.WriteLine("Performing current time update...");
                    sqlInteraction.CurrentTimeUpdate();
                }

                Console.WriteLine($"Update for list '{listName}' completed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating list '{listName}': {ex.Message}");
            }
        }
    }
}