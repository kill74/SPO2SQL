using Bring.Sharepoint;
using Bring.Sqlserver;
using Bring.XmlConfig;
using Bring.SPODataQuality;
using Microsoft.SharePoint.Client;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Data.SqlClient;

namespace Bring.SPODataQuality
{
    internal class RefreshSPOLists
    {
        // Main entry point of the application, executed when the program starts
        private static void Main(string[] args)
        {
            Logger.Log(1, "DEBUG: Usig the Default config");
            string configPath = "XmlConfig\\UserConfig.xml"; // Default path for the configuration file

            int verbose = 0;
            bool diagnostic = false;

            foreach (var arg in args)
            {
                if (arg.StartsWith("--config="))
                {
                    configPath = arg.Substring("--config=".Length);
                }
                else if (arg.StartsWith("--verbose="))
                {
                    if (int.TryParse(arg.Substring("--verbose=".Length), out int v) && v >= 0 && v <= 3)
                        verbose = v;
                }
                else if (arg.ToLower() == "diagnostic")
                {
                    diagnostic = true;
                }
            }

            if (diagnostic && verbose == 0)
                verbose = 1;

            Logger.VerboseLevel = verbose;

            Bring.XmlConfig.ConfigurationReader.SetConfigPath(configPath);

            try
            {
                Logger.Log(1, "DEBUG: Starting Main");
                Logger.Log(2, "CURRENT TIME: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));

                try
                {
                    TestSQLConnection();
                    Logger.Log(1, "DEBUG: Main: SQL connection test completed");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR: SQL connection test failed.");
                    Console.WriteLine("Exception: " + ex.Message);
                    Console.WriteLine("Stack Trace: " + ex.StackTrace);
                    return;
                }

                (string username, string password) credentials;
                try
                {
                    credentials = ConfigurationReader.GetSharePointCredentials();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR: Failed to retrieve SharePoint credentials.");
                    Console.WriteLine("Exception: " + ex.Message);
                    Console.WriteLine("Stack Trace: " + ex.StackTrace);
                    return;
                }

                SPOUser spoUser;
                try
                {
                    spoUser = new SPOUser(credentials.username, credentials.password);
                    Logger.Log(1, "DEBUG: Main: SPOUser created");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR: Failed to create SPOUser.");
                    Console.WriteLine("Exception: " + ex.Message);
                    Console.WriteLine("Stack Trace: " + ex.StackTrace);
                    return;
                }

                SPOList list1 = null, list2 = null;
                try
                {
                    list1 = new SPOList();
                    list1.SPOUser = spoUser;
                    Logger.Log(3, "DEBUG: Main: First SPOList configured");

                    list2 = new SPOList();
                    list2.SPOUser = spoUser;
                    Logger.Log(1, "DEBUG: Main: Second SPOList configured");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR: Failed to configure SPOList(s).");
                    Console.WriteLine("Exception: " + ex.Message);
                    Console.WriteLine("Stack Trace: " + ex.StackTrace);
                    return;
                }

                try
                {
                    if ((uint)args.Length > 0U)
                    {
                        string lower = args[0].ToLower();
                        Logger.Log(1, "DEBUG: Main: Received argument - " + lower);

                        if (lower == "daily")
                        {
                            Logger.Log(1, "DEBUG: Main: Executing daily");
                            try
                            {
                                RefreshSQLLists.SPOtoSQLUpdate(true);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("ERROR: Exception during daily update.");
                                Console.WriteLine("Exception: " + ex.Message);
                                Console.WriteLine("Stack Trace: " + ex.StackTrace);
                            }
                        }
                        else if (lower == "monthly")
                        {
                            Logger.Log(1, "DEBUG: Main: Executing monthly");
                            try
                            {
                                RefreshSQLLists.SPOtoSQLUpdate(false);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("ERROR: Exception during monthly update.");
                                Console.WriteLine("Exception: " + ex.Message);
                                Console.WriteLine("Stack Trace: " + ex.StackTrace);
                            }
                        }
                        else
                        {
                            Logger.Log(2, "Unrecognized argument, please use daily or monthly as the argument");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR: Exception while processing arguments.");
                    Console.WriteLine("Exception: " + ex.Message);
                    Console.WriteLine("Stack Trace: " + ex.StackTrace);
                }

                Logger.Log(2, "End of requests.");
                Logger.Log(2, "");
            }
            catch (Exception ex)
            {
                Console.WriteLine("FATAL ERROR: An error occurred in Main.");
                Console.WriteLine("Exception: " + ex.Message);
                Console.WriteLine("Stack Trace: " + ex.StackTrace);
            }
        }

        private static void TestSQLConnection()
        {
            Logger.Log(2, "Testing SQL Server connection...");
            try
            {
                string connectionString = ConfigurationReader.GetSqlConnectionString();
                Logger.Log(2, "Attempting to connect to SQL server...");

                using (var connection = new SqlConnection(connectionString))
                {
                    try
                    {
                        connection.Open();
                    }
                    catch (SqlException ex) when (ex.Number == -1 || ex.Number == 10060 || ex.Number == 0 || ex.Number == 53)
                    {
                        Console.WriteLine("ERROR: Unable to connect to the SQL Server.");
                        Console.WriteLine("Please check if your VPN connection is active and try again.");
                        Console.WriteLine("Technical details: " + ex.Message);
                        Console.WriteLine("Press any key to exit...");
                        Console.ReadKey();
                        Environment.Exit(1);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("ERROR: Unexpected error while opening SQL connection.");
                        Console.WriteLine("Exception: " + ex.Message);
                        throw;
                    }

                    Logger.Log(2, "Server: " + connection.DataSource);
                    Logger.Log(2, "Database: " + connection.Database);
                    Logger.Log(2, "SQL connection established successfully!");

                    // Basic permissions test
                    using (var command = connection.CreateCommand())
                    {
                        // SELECT test
                        try
                        {
                            command.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES";
                            int tableCount = (int)command.ExecuteScalar();
                            Logger.Log(2, "Number of tables in database: " + tableCount);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("ERROR: Failed to execute SELECT COUNT(*) on INFORMATION_SCHEMA.TABLES.");
                            Console.WriteLine("Exception: " + ex.Message);
                        }

                        // CREATE TABLE permission test
                        try
                        {
                            command.CommandText = "CREATE TABLE #TempTest (ID int); DROP TABLE #TempTest;";
                            command.ExecuteNonQuery();
                            Logger.Log(2, "CREATE TABLE permission: OK");
                        }
                        catch (SqlException ex)
                        {
                            Logger.Log(2, "Warning: No CREATE TABLE permission: " + ex.Message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("ERROR: Unexpected error during CREATE TABLE permission test.");
                            Console.WriteLine("Exception: " + ex.Message);
                        }
                    }
                }
            }

            catch (SqlException ex)
            {
                Console.WriteLine("SQL connection error: " + ex.Message);
                Console.WriteLine("Error number: " + ex.Number);
                Console.WriteLine("State: " + ex.State);
                Console.WriteLine("Procedure: " + ex.Procedure);
                Console.WriteLine("The Execution is stoping.");
                throw; // Re-throw to stop execution
            }
            catch (Exception ex)
            {
                Console.WriteLine("General error testing SQL connection: " + ex.Message);
                throw;
            }
        }


        // Method to retrieve and display all lists from a specific SharePoint site
        public static void GetAllLists()
        {
            Logger.Log(1, "DEBUG: Entering GetAllLists");
            try
            {
                // Get SharePoint credentials
                var (username, password) = ConfigurationReader.GetSharePointCredentials();
                SPOUser spoUser = new SPOUser(username, password);
                // Set up a context for the SharePoint site named "seed"
                Context context = new Context()
                {
                    Site = "seed",
                    SPOUser = spoUser
                };
                // Iterate through all lists in the SharePoint site
                foreach (List allList in (ClientObjectCollection<List>)context.GetAllLists())
                {
                    try
                    {
                        Logger.Log(1, "DEBUG: Loading list - " + allList.Title);
                        // Load the IsSystemList property to determine if the list is a system list
                        context.Ctx.Load<List>(allList, new Expression<Func<List, object>>[1]
                        {
                            (Expression<Func<List, object>>) (l => (object) l.IsSystemList)
                        });
                        context.Ctx.ExecuteQuery(); // Execute the query to retrieve the data
                        Logger.Log(2, "List Name: " + allList.Title + "; is: " + allList.IsSystemList.ToString());
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("ERROR: Failed to load or display list '" + allList.Title + "'.");
                        Console.WriteLine("Exception: " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: Exception in GetAllLists.");
                Console.WriteLine("Exception: " + ex.Message);
                Console.WriteLine("Stack Trace: " + ex.StackTrace);
            }
        }

        // Debugging method to inspect the properties of a specific SharePoint list
        private static void SPODebug(string listName, string ctxURL, SPOUser user)
        {
            Logger.Log(1, "DEBUG: Entering SPODebug");
            try
            {
                // Create a SharePoint list object with a CAML query limiting to 1 item
                SPOList spoList = new SPOList
                {
                    Name = listName,
                    Site = ctxURL,
                    SPOUser = user,
                    CAMLQuery = "<View><RowLimit>1</RowLimit></View>"
                };

                Logger.Log(1, "DEBUG: Executing Build");
                try
                {
                    spoList.Build(); // Build the list to load its data
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR: Failed to build SPOList.");
                    Console.WriteLine("Exception: " + ex.Message);
                    return;
                }

                Logger.Log(1, "DEBUG: Executing PropsToString");
                try
                {
                    // Print the properties of the first item in the list for debugging
                    if (spoList.ItemCollection != null && spoList.ItemCollection.Count > 0)
                        spoList.PropsToString(spoList.ItemCollection[0]);
                    else
                        Logger.Log(2, "No items found in the list.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR: Failed to print properties of the first item.");
                    Console.WriteLine("Exception: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: Exception in SPODebug.");
                Console.WriteLine("Exception: " + ex.Message);
                Console.WriteLine("Stack Trace: " + ex.StackTrace);
            }
        }

        // Method to synchronize data from a source SharePoint list to a destination list
        private static void RefreshListsSPO(SPOList sourceList, SPOList destList)
        {
            try
            {
                Logger.Log(1, "DEBUG: Starting RefreshListsSPO");

                try
                {
                    // Build the source list to load its data
                    sourceList.Build();
                    Logger.Log(1, "DEBUG: sourceList.Build completed");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR: Failed to build source SPOList.");
                    Console.WriteLine("Exception: " + ex.Message);
                    return;
                }

                try
                {
                    // Build the destination list to load its data
                    destList.Build();
                    Logger.Log(1, "DEBUG: destList.Build completed");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR: Failed to build destination SPOList.");
                    Console.WriteLine("Exception: " + ex.Message);
                    return;
                }

                int num1 = 0; // Last ID in source list
                int num2 = 0; // Last ID in destination list

                string[,] actualFields;
                try
                {
                    // Get the field mappings between the source and destination lists
                    actualFields = GetActualFields(sourceList, destList);
                    Logger.Log(1, "DEBUG: Fields obtained");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR: Failed to get actual fields mapping.");
                    Console.WriteLine("Exception: " + ex.Message);
                    return;
                }

                try
                {
                    // Determine the last ID in the source list
                    if ((uint)sourceList.ItemCollection.Count > 0U)
                        num1 = (int)sourceList.ItemCollection[sourceList.ItemCollection.Count - 1]["ID"];
                    // Determine the last ID in the destination list
                    if ((uint)destList.ItemCollection.Count > 0U)
                        num2 = (int)destList.ItemCollection[destList.ItemCollection.Count - 1]["ID"];
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR: Failed to determine last IDs in source/destination lists.");
                    Console.WriteLine("Exception: " + ex.Message);
                }

                // If the destination list has fewer items, add new items to match the source
                if (num2 < num1)
                {
                    Logger.Log(1, "DEBUG: Adding new items");
                    try
                    {
                        do
                        {
                            destList.AddItem(); // Add a new item to the destination list
                            ++num2;
                        }
                        while (num2 < num1);

                        Logger.Log(2, "Adding new items...");
                        destList.Update(); // Update the destination list with the new items
                        Logger.Log(2, "Done adding items.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("ERROR: Failed to add or update new items in destination list.");
                        Console.WriteLine("Exception: " + ex.Message);
                    }
                }

                try
                {
                    // Update existing items in the destination list with data from the source
                    for (int index1 = 0; index1 < sourceList.ItemCollection.Count; ++index1)
                    {
                        int id = (int)sourceList.ItemCollection[index1]["ID"];
                        // Copy field values from source to destination based on the field mappings
                        for (int index2 = 0; index2 < actualFields.Length / 2; ++index2)
                        {
                            try
                            {
                                destList.ItemCollection.GetById(id)[actualFields[index2, 0]] = sourceList.ItemCollection[index1][actualFields[index2, 1]];
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("ERROR: Failed to copy field '" + actualFields[index2, 1] + "' to '" + actualFields[index2, 0] + "' for item ID " + id + ".");
                                Console.WriteLine("Exception: " + ex.Message);
                            }
                        }
                        try
                        {
                            destList.ItemCollection.GetById(id).Update(); // Update the item in the destination list
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("ERROR: Failed to update item ID " + id + " in destination list.");
                            Console.WriteLine("Exception: " + ex.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR: Exception during item synchronization.");
                    Console.WriteLine("Exception: " + ex.Message);
                }

                try
                {
                    // Execute the query to apply all changes to the SharePoint site
                    destList.Ctx.ExecuteQuery();
                    Logger.Log(2, sourceList.Site + " " + sourceList.Name + " -> " + destList.Site + " " + destList.Name + ": Done!");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR: Failed to execute query on destination list context.");
                    Console.WriteLine("Exception: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                // Catch and display any errors during the sync process
                Console.WriteLine("ERROR in RefreshListsSPO: " + ex.Message);
                Console.WriteLine("Stack Trace: " + ex.StackTrace);
            }
        }

        // Helper method to create a mapping of fields between two lists based on their titles
        private static string[,] GetActualFields(SPOList listone, SPOList listtwo)
        {
            Logger.Log(1, "DEBUG: Entering GetActualFields");
            try
            {
                // Get fields from both lists
                List<Field> fields1 = GetFields(listone);
                List<Field> fields2 = GetFields(listtwo);

                // Create a 2D array to store the field mappings (internal names)
                string[,] strArray = new string[fields1.Count, 2];
                int index1 = 0;
                int index2 = 0;

                // Match fields by title and store their internal names
                foreach (Field field1 in fields1)
                {
                    Field field2;
                    do
                    {
                        field2 = fields2[index2];
                        if (field1.Title == field2.Title)
                        {
                            strArray[index1, 0] = field2.InternalName; // Destination field
                            strArray[index1, 1] = field1.InternalName; // Source field
                            Logger.Log(1, "DEBUG: Match found - " + field1.Title);
                        }
                        ++index2;
                    }
                    while (field1.Title != field2.Title && index2 < fields2.Count);

                    ++index1;
                    index2 = 0; // Reset index2 for the next field
                }

                return strArray; // Return the field mappings
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: Exception in GetActualFields.");
                Console.WriteLine("Exception: " + ex.Message);
                throw;
            }
        }

        // Helper method to retrieve fields from a SharePoint list, excluding base type fields except "Title"
        private static List<Field> GetFields(SPOList list)
        {
            Logger.Log(1, "DEBUG: Entering GetFields");
            List<Field> fieldList = new List<Field>();
            try
            {
                // Iterate through all fields in the list
                foreach (Field field in (ClientObjectCollection<Field>)list.Fields)
                {
                    // Include fields that are not from the base type or are the "Title" field
                    if (!field.FromBaseType || field.InternalName == "Title")
                    {
                        fieldList.Add(field);
                        Logger.Log(1, "DEBUG: Field added - " + field.Title);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: Exception in GetFields.");
                Console.WriteLine("Exception: " + ex.Message);
                throw;
            }

            return fieldList; // Return the list of fields
        }
    }
}