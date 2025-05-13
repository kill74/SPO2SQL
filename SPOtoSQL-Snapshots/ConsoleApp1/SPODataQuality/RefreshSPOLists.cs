using Bring.Sharepoint;
using Bring.Sqlserver;
using Bring.XmlConfig;
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
            try
            {
                // Print debug information and the current timestamp
                Console.WriteLine("DEBUG: Starting Main");
                Console.WriteLine("CURRENT TIME: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));

                // Test SQL connection before proceeding
                TestSQLConnection();
                Console.WriteLine("DEBUG: Main: SQL connection test completed");

                // Retrieve SharePoint credentials (username and password) from a configuration source
                var (username, password) = ConfigurationReader.GetSharePointCredentials();
                SPOUser spoUser = new SPOUser(username, password); // Create a SharePoint user object
                Console.WriteLine("DEBUG: Main: SPOUser created"); // When Username and Password are filled correctly, this line will be printed

                // Initialize two SharePoint list objects and assign the user credentials to them
                var list1 = new SPOList();
                list1.SPOUser = spoUser;
                Console.WriteLine("DEBUG: Main: First SPOList configured");

                var list2 = new SPOList();
                list2.SPOUser = spoUser;
                Console.WriteLine("DEBUG: Main: Second SPOList configured");

                // Check if command-line arguments are provided
                if ((uint)args.Length > 0U)
                {
                    string lower = args[0].ToLower(); // Convert the first argument to lowercase
                    Console.WriteLine("DEBUG: Main: Received argument - " + lower);

                    // Decide which update to perform based on the argument
                    if (lower == "daily")
                    {
                        // Perform a daily update from SharePoint to SQL Server
                        Console.WriteLine("DEBUG: Main: Executing daily");
                        RefreshSQLLists.SPOtoSQLUpdate(true);
                    }
                    else if (lower == "monthly")
                    {
                        // Perform a monthly update from SharePoint to SQL Server
                        Console.WriteLine("DEBUG: Main: Executing monthly");
                        RefreshSQLLists.SPOtoSQLUpdate(false);
                    }
                    else
                    {
                        // Handle unrecognized arguments
                        Console.WriteLine("Unrecognized argument, please use daily or monthly as the argument");
                    }
                }

                // Indicate the end of processing
                Console.WriteLine("End of requests.");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                // Catch and display any errors that occur during execution
                Console.WriteLine("An error occurred: " + ex.Message);
                Console.WriteLine("Stack Trace: " + ex.StackTrace);
            }
        }

        private static void TestSQLConnection()
        {
            Console.WriteLine("Testing SQL Server connection...");
            try
            {
                string connectionString = ConfigurationReader.GetSqlConnectionString();
                Console.WriteLine($"Attempting to connect to SQL server...");

                using (var connection = new SqlConnection(connectionString))
                {
                    try // Nova verificação específica para VPN
                    {
                        connection.Open();
                    }
                    catch (SqlException ex) when (ex.Number == -1 || ex.Number == 10060 || ex.Number == 0 || ex.Number == 53)
                    {
                        Console.WriteLine("ERROR: Unable to connect to the SQL Server.");
                        Console.WriteLine("Please check if your VPN connection is active and try again.");
                        Console.WriteLine($"Technical details: {ex.Message}");
                        Console.WriteLine("Press any key to exit...");
                        Console.ReadKey();
                        Environment.Exit(1);
                    }

                    Console.WriteLine($"Server: {connection.DataSource}");
                    Console.WriteLine($"Database: {connection.Database}");
                    Console.WriteLine("SQL connection established successfully!");

                    // Basic permissions test
                    using (var command = connection.CreateCommand())
                    {
                        // SELECT test
                        command.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES";
                        int tableCount = (int)command.ExecuteScalar();
                        Console.WriteLine($"Number of tables in database: {tableCount}");

                        // CREATE TABLE permission test
                        try
                        {
                            command.CommandText = "CREATE TABLE #TempTest (ID int); DROP TABLE #TempTest;";
                            command.ExecuteNonQuery();
                            Console.WriteLine("CREATE TABLE permission: OK");
                        }
                        catch (SqlException ex)
                        {
                            Console.WriteLine($"Warning: No CREATE TABLE permission: {ex.Message}");
                        }
                    }
                }
            }

            catch (SqlException ex)
            {
                Console.WriteLine($"SQL connection error: {ex.Message}");
                Console.WriteLine($"Error number: {ex.Number}");
                Console.WriteLine($"State: {ex.State}");
                Console.WriteLine($"Procedure: {ex.Procedure}");
                Console.WriteLine("The Execution is stoping.");
                throw; // Re-throw to stop execution
            }
            catch (Exception ex)
            {
                Console.WriteLine($"General error testing SQL connection: {ex.Message}");
                throw;
            }
        }


        // Method to retrieve and display all lists from a specific SharePoint site
        public static void GetAllLists()
        {
            Console.WriteLine("DEBUG: Entering GetAllLists");
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
                Console.WriteLine("DEBUG: Loading list - " + allList.Title);
                // Load the IsSystemList property to determine if the list is a system list
                context.Ctx.Load<List>(allList, new Expression<Func<List, object>>[1]
                {
                    (Expression<Func<List, object>>) (l => (object) l.IsSystemList)
                });
                context.Ctx.ExecuteQuery(); // Execute the query to retrieve the data
                Console.WriteLine("List Name: " + allList.Title + "; is: " + allList.IsSystemList.ToString());
            }
        }

        // Debugging method to inspect the properties of a specific SharePoint list
        private static void SPODebug(string listName, string ctxURL, SPOUser user)
        {
            Console.WriteLine("DEBUG: Entering SPODebug");
            // Create a SharePoint list object with a CAML query limiting to 1 item
            SPOList spoList = new SPOList
            {
                Name = listName,
                Site = ctxURL,
                SPOUser = user,
                CAMLQuery = "<View><RowLimit>1</RowLimit></View>"
            };

            Console.WriteLine("DEBUG: Executing Build");
            spoList.Build(); // Build the list to load its data

            Console.WriteLine("DEBUG: Executing PropsToString");
            // Print the properties of the first item in the list for debugging
            spoList.PropsToString(spoList.ItemCollection[0]);
        }

        // Method to synchronize data from a source SharePoint list to a destination list
        private static void RefreshListsSPO(SPOList sourceList, SPOList destList)
        {
            try
            {
                Console.WriteLine("DEBUG: Starting RefreshListsSPO");

                // Build the source list to load its data
                sourceList.Build();
                Console.WriteLine("DEBUG: sourceList.Build completed");

                // Build the destination list to load its data
                destList.Build();
                Console.WriteLine("DEBUG: destList.Build completed");

                int num1 = 0; // Last ID in source list
                int num2 = 0; // Last ID in destination list

                // Get the field mappings between the source and destination lists
                string[,] actualFields = GetActualFields(sourceList, destList);
                Console.WriteLine("DEBUG: Fields obtained");

                // Determine the last ID in the source list
                if ((uint)sourceList.ItemCollection.Count > 0U)
                    num1 = (int)sourceList.ItemCollection[sourceList.ItemCollection.Count - 1]["ID"];
                // Determine the last ID in the destination list
                if ((uint)destList.ItemCollection.Count > 0U)
                    num2 = (int)destList.ItemCollection[destList.ItemCollection.Count - 1]["ID"];

                // If the destination list has fewer items, add new items to match the source
                if (num2 < num1)
                {
                    Console.WriteLine("DEBUG: Adding new items");
                    do
                    {
                        destList.AddItem(); // Add a new item to the destination list
                        ++num2;
                    }
                    while (num2 < num1);

                    Console.WriteLine("Adding new items...");
                    destList.Update(); // Update the destination list with the new items
                    Console.WriteLine("Done adding items.");
                }

                // Update existing items in the destination list with data from the source
                for (int index1 = 0; index1 < sourceList.ItemCollection.Count; ++index1)
                {
                    int id = (int)sourceList.ItemCollection[index1]["ID"];
                    // Copy field values from source to destination based on the field mappings
                    for (int index2 = 0; index2 < actualFields.Length / 2; ++index2)
                        destList.ItemCollection.GetById(id)[actualFields[index2, 0]] = sourceList.ItemCollection[index1][actualFields[index2, 1]];
                    destList.ItemCollection.GetById(id).Update(); // Update the item in the destination list
                }

                // Execute the query to apply all changes to the SharePoint site
                destList.Ctx.ExecuteQuery();
                Console.WriteLine(sourceList.Site + " " + sourceList.Name + " -> " + destList.Site + " " + destList.Name + ": Done!");
            }
            catch (Exception ex)
            {
                // Catch and display any errors during the sync process
                Console.WriteLine("ERROR in RefreshListsSPO: " + ex.Message);
            }
        }

        // Helper method to create a mapping of fields between two lists based on their titles
        private static string[,] GetActualFields(SPOList listone, SPOList listtwo)
        {
            Console.WriteLine("DEBUG: Entering GetActualFields");

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
                        Console.WriteLine($"DEBUG: Match found - {field1.Title}");
                    }
                    ++index2;
                }
                while (field1.Title != field2.Title && index2 < fields2.Count);

                ++index1;
                index2 = 0; // Reset index2 for the next field
            }

            return strArray; // Return the field mappings
        }

        // Helper method to retrieve fields from a SharePoint list, excluding base type fields except "Title"
        private static List<Field> GetFields(SPOList list)
        {
            Console.WriteLine("DEBUG: Entering GetFields");

            List<Field> fieldList = new List<Field>();
            // Iterate through all fields in the list
            foreach (Field field in (ClientObjectCollection<Field>)list.Fields)
            {
                // Include fields that are not from the base type or are the "Title" field
                if (!field.FromBaseType || field.InternalName == "Title")
                {
                    fieldList.Add(field);
                    Console.WriteLine($"DEBUG: Field added - {field.Title}");
                }
            }

            return fieldList; // Return the list of fields
        }
    }
}