using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using Microsoft.Data.SqlClient;
using Microsoft.SharePoint.Client;
using SPO2SQL.Logging;
using SPO2SQL.Security;
using SPO2SQL.SharePoint;
using SPO2SQL.SqlServer;
using SPO2SQL.XmlConfig;

namespace SPO2SQL.SPODataQuality;

internal sealed class RefreshSPOLists
{
    private static void Main(string[] args)
    {
        try
        {
            InitializeApplication(args);

            if (args.Any(a => a.Equals("--setup-secrets", StringComparison.OrdinalIgnoreCase)))
            {
                RunSetupSecrets();
                return;
            }

            RunMainWorkflow();
        }
        catch (Exception ex)
        {
            HandleFatalError(ex);
        }
    }

    private static void RunSetupSecrets()
    {
        Console.WriteLine("=== Secure Credential Setup ===");
        Console.WriteLine("Credentials will be encrypted and stored at:");
        Console.WriteLine("  %APPDATA%\\SPO2SQL\\credentials.enc");
        Console.WriteLine();

        Console.Write("SharePoint Username: ");
        string username = Console.ReadLine()?.Trim();
        Console.Write("SharePoint Password: ");
        string password = Console.ReadLine()?.Trim();
        Console.Write("SQL Connection String: ");
        string connString = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            Console.WriteLine("ERROR: Username and password are required.");
            Environment.Exit(1);
        }

        try
        {
            DpapiCredentialProvider.SaveCredentials(username, password, connString ?? "");
            Console.WriteLine("Credentials saved securely.");
        }
        catch (PlatformNotSupportedException)
        {
            Console.WriteLine("ERROR: DPAPI encryption is not available on this platform.");
            Console.WriteLine("Use environment variables instead:");
            Console.WriteLine("  set SPO_USERNAME=" + username);
            Console.WriteLine("  set SPO_PASSWORD=...");
            Console.WriteLine("  set SQL_CONNECTION_STRING=...");
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR: Failed to save credentials: " + ex.Message);
            Environment.Exit(1);
        }
    }

    private static void InitializeApplication(string[] args)
    {
        Logger.Log(1, "DEBUG: Using the Default config");
        string configPath = "XmlConfig\\UserConfig.xml";

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
                {
                    verbose = v;
                }
            }
            else if (arg.ToLower() == "diagnostic")
            {
                diagnostic = true;
            }
        }

        if (diagnostic && verbose == 0)
        {
            verbose = 1;
        }

        Logger.VerboseLevel = verbose;

        ConfigurationReader.SetConfigPath(configPath);

        Logger.Log(1, "DEBUG: Application initialized");
        Logger.Log(2, "CURRENT TIME: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
    }

    private static void RunMainWorkflow()
    {
        try
        {
            TestSQLConnection();
            Logger.Log(1, "DEBUG: SQL connection test completed");

            var credentials = ConfigurationReader.GetSharePointCredentials();
            if (string.IsNullOrEmpty(credentials.Username) || string.IsNullOrEmpty(credentials.Password))
            {
                throw new InvalidOperationException("Failed to retrieve SharePoint credentials from configuration");
            }

            if (!TwoFactorAuth.PerformVerification())
            {
                Console.WriteLine("2FA verification failed. Exiting.");
                Environment.Exit(1);
            }

            using (var spoUser = new SPOUser(credentials.Username, credentials.Password))
            {
                Logger.Log(1, "DEBUG: SPOUser created");

                Logger.Log(3, "DEBUG: SPOList objects configured");

                ProcessCommandLineArguments();

                Logger.Log(2, "End of requests.");
                Logger.Log(2, "");
            }
        }
        catch (Exception ex)
        {
            throw new ApplicationException("Error in main workflow", ex);
        }
    }

    private static void ProcessCommandLineArguments()
    {
        Logger.Log(1, "DEBUG: Processing command-line arguments");
        Logger.Log(1, "DEBUG: Command-line arguments processed");
    }

    private static void HandleFatalError(Exception ex)
    {
        Console.WriteLine("FATAL ERROR: An unexpected error occurred.");
        Console.WriteLine($"Exception: {ex.GetType().Name}: {ex.Message}");
        Console.WriteLine("Stack Trace: " + ex.StackTrace);
        Environment.Exit(1);
    }

    private static void TestSQLConnection()
    {
        Logger.Log(2, "Testing SQL Server connection...");
        try
        {
            string connectionString = ConfigurationReader.GetSqlConnectionString();
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("SQL connection string is not configured");
            }

            Logger.Log(2, "Attempting to connect to SQL server...");

            using (var connection = new SqlConnection(connectionString))
            {
                try
                {
                    connection.Open();
                }
                catch (SqlException ex) when (IsNetworkRelatedSqlError(ex.Number))
                {
                    Console.WriteLine("ERROR: Unable to connect to the SQL Server.");
                    Console.WriteLine("Please check if your VPN connection is active and try again.");
                    Console.WriteLine("Technical details: " + ex.Message);
                    Console.WriteLine("Press any key to exit...");
                    if (Console.KeyAvailable || !Console.IsInputRedirected)
                    {
                        try
                        {
                            Console.ReadKey(true);
                        }
                        catch (InvalidOperationException)
                        {
                        }
                    }
                    else
                    {
                        Thread.Sleep(2000);
                    }
                    Environment.Exit(1);
                }
                catch (SqlException ex)
                {
                    Console.WriteLine($"SQL error: {ex.Number} - {ex.Message}");
                    throw;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR: Unexpected error while opening SQL connection.");
                    Console.WriteLine($"Exception: {ex.GetType().Name}: {ex.Message}");
                    throw;
                }

                Logger.Log(2, $"Server: {connection.DataSource}");
                Logger.Log(2, $"Database: {connection.Database}");
                Logger.Log(2, "SQL connection established successfully!");

                using (var command = connection.CreateCommand())
                {
                    try
                    {
                        command.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES";
                        object result = command.ExecuteScalar();
                        if (result != null)
                        {
                            int tableCount = Convert.ToInt32(result);
                            Logger.Log(2, $"Number of tables in database: {tableCount}");
                        }
                        else
                        {
                            Logger.Log(2, "Number of tables in database: 0 (null result)");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("ERROR: Failed to execute SELECT COUNT(*) on INFORMATION_SCHEMA.TABLES.");
                        Console.WriteLine($"Exception: {ex.GetType().Name}: {ex.Message}");
                    }

                    try
                    {
                        command.CommandText = "CREATE TABLE #TempTest (ID int); DROP TABLE #TempTest;";
                        command.ExecuteNonQuery();
                        Logger.Log(2, "CREATE TABLE permission: OK");
                    }
                    catch (SqlException ex)
                    {
                        Logger.Log(2, $"Warning: No CREATE TABLE permission: {ex.Number} - {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("ERROR: Unexpected error during CREATE TABLE permission test.");
                        Console.WriteLine($"Exception: {ex.GetType().Name}: {ex.Message}");
                    }
                }
            }
        }
        catch (SqlException ex)
        {
            Console.WriteLine($"SQL connection error: {ex.Number} - {ex.Message}");
            Console.WriteLine("Error details:");
            Console.WriteLine($"  Number: {ex.Number}");
            Console.WriteLine($"  State: {ex.State}");
            if (!string.IsNullOrEmpty(ex.Procedure))
            {
                Console.WriteLine($"  Procedure: {ex.Procedure}");
            }
            Console.WriteLine("The execution is stopping.");
            throw;
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"Configuration error: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"General error testing SQL connection: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }

    private static bool IsNetworkRelatedSqlError(int errorNumber)
    {
        switch (errorNumber)
        {
            case -2:
            case -1:
            case 0:
            case 53:
            case 64:
            case 87:
            case 10060:
            case 10061:
            case 11001:
                return true;
            default:
                return false;
        }
    }

    public static void GetAllLists()
    {
        Logger.Log(1, "DEBUG: Entering GetAllLists");
        try
        {
            var (username, password) = ConfigurationReader.GetSharePointCredentials();
            using (SPOUser spoUser = new SPOUser(username, password))
            {
                Context context = new Context()
                {
                    Site = "seed",
                    SPOUser = spoUser
                };
                foreach (Microsoft.SharePoint.Client.List allList in context.GetAllLists())
                {
                    try
                    {
                        Logger.Log(1, "DEBUG: Loading list - " + allList.Title);
                        context.Ctx.Load(allList, l => l.IsSystemList);
                        context.Ctx.ExecuteQuery();
                        Logger.Log(2, "List Name: " + allList.Title + "; is: " + allList.IsSystemList.ToString());
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("ERROR: Failed to load or display list '" + allList.Title + "'.");
                        Console.WriteLine("Exception: " + ex.Message);
                    }
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

    private static void SPODebug(string listName, string ctxURL, SPOUser user)
    {
        Logger.Log(1, "DEBUG: Entering SPODebug");
        try
        {
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
                spoList.Build();
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: Failed to build SPOList.");
                Console.WriteLine("Exception: " + ex.Message);
                return;
            }

            Logger.Log(1, "DEBUG: Executing PrintItemProperties");
            try
            {
                if (spoList.ItemCollection != null && spoList.ItemCollection.Count > 0)
                {
                    spoList.PrintItemProperties(spoList.ItemCollection[0]);
                }
                else
                {
                    Logger.Log(2, "No items found in the list.");
                }
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

    private static void RefreshListsSPO(SPOList sourceList, SPOList destList)
    {
        try
        {
            Logger.Log(1, "DEBUG: Starting RefreshListsSPO");

            try
            {
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
                destList.Build();
                Logger.Log(1, "DEBUG: destList.Build completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: Failed to build destination SPOList.");
                Console.WriteLine("Exception: " + ex.Message);
                return;
            }

            int num1 = 0;
            int num2 = 0;

            string[,] actualFields;
            try
            {
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
                if ((uint)sourceList.ItemCollection.Count > 0U)
                {
                    num1 = (int)sourceList.ItemCollection[sourceList.ItemCollection.Count - 1]["ID"];
                }

                if ((uint)destList.ItemCollection.Count > 0U)
                {
                    num2 = (int)destList.ItemCollection[destList.ItemCollection.Count - 1]["ID"];
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: Failed to determine last IDs in source/destination lists.");
                Console.WriteLine("Exception: " + ex.Message);
            }

            if (num2 < num1)
            {
                Logger.Log(1, "DEBUG: Adding new items");
                try
                {
                    do
                    {
                        destList.AddItem();
                        ++num2;
                    }
                    while (num2 < num1);

                    Logger.Log(2, "Adding new items...");
                    destList.Update();
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
                for (int index1 = 0; index1 < sourceList.ItemCollection.Count; ++index1)
                {
                    int id = (int)sourceList.ItemCollection[index1]["ID"];
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
                        destList.ItemCollection.GetById(id).Update();
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
            Console.WriteLine("ERROR in RefreshListsSPO: " + ex.Message);
            Console.WriteLine("Stack Trace: " + ex.StackTrace);
        }
    }

    private static string[,] GetActualFields(SPOList listone, SPOList listtwo)
    {
        Logger.Log(1, "DEBUG: Entering GetActualFields");
        try
        {
            List<Field> fields1 = GetFields(listone);
            List<Field> fields2 = GetFields(listtwo);

            string[,] strArray = new string[fields1.Count, 2];
            for (int i = 0; i < fields1.Count; i++)
            {
                strArray[i, 0] = "";
                strArray[i, 1] = "";
            }

            int index1 = 0;
            int index2 = 0;

            foreach (Field field1 in fields1)
            {
                bool found = false;
                while (index2 < fields2.Count)
                {
                    Field field2 = fields2[index2];
                    if (field1.Title == field2.Title)
                    {
                        strArray[index1, 0] = field2.InternalName;
                        strArray[index1, 1] = field1.InternalName;
                        found = true;
                        Logger.Log(1, "DEBUG: Match found - " + field1.Title);
                    }
                    ++index2;
                    if (found)
                    {
                        break;
                    }
                }

                ++index1;
                index2 = 0;
            }

            return strArray;
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR: Exception in GetActualFields.");
            Console.WriteLine("Exception: " + ex.Message);
            throw;
        }
    }

    private static List<Field> GetFields(SPOList list)
    {
        Logger.Log(1, "DEBUG: Entering GetFields");
        List<Field> fieldList = new List<Field>();
        try
        {
            foreach (Field field in (ClientObjectCollection<Field>)list.Fields)
            {
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

        return fieldList;
    }
}
