using SPO2SQL.Logging;
using SPO2SQL.SharePoint;
using SPO2SQL.XmlConfig;

namespace SPO2SQL.SqlServer;

internal sealed class RefreshSQLLists
{
    public static void SPOtoSQLUpdate(bool daily, CancellationToken cancellationToken = default)
    {
        Logger.Log(2, "SPOtoSQLUpdate: Starting SPO to SQL update. Daily: " + daily);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (username, password) = ConfigurationReader.GetSharePointCredentials();
            Logger.Log(2, $"SPOtoSQLUpdate: Username retrieved successfully");

            if (!TwoFactorAuth.PerformVerification())
            {
                Console.WriteLine("2FA verification failed. Exiting.");
                return;
            }

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

            using (user)
            {
                var listConfigs = ConfigurationReader.GetListConfigurations();
                if (listConfigs != null)
                {
                    foreach (var kvp in listConfigs)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var listName = kvp.Key;
                        var config = kvp.Value;
                        if (config.Ignore)
                        {
                            continue;
                        }

                        string ctxURL = config.SharepointList;
                        Logger.Log(1, $"SPOtoSQLUpdate: Processing list: {listName} with URL: {ctxURL}");
                        try
                        {
                            RefreshListsSQL(listName, ctxURL, user, daily, cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            Logger.Log(2, $"SPOtoSQLUpdate: Cancelled while processing list '{listName}'.");
                            throw;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"SPOtoSQLUpdate: ERROR - Exception while updating list '{listName}'.");
                            Console.WriteLine("Exception: " + ex.Message);
                            Console.WriteLine("Stack Trace: " + ex.StackTrace);
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Log(2, "SPOtoSQLUpdate: Operation was cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine("SPOtoSQLUpdate: FATAL ERROR - Exception during SPO to SQL update process.");
            Console.WriteLine("Exception: " + ex.Message);
            Console.WriteLine("Stack Trace: " + ex.StackTrace);
        }

        Logger.Log(2, "SPOtoSQLUpdate: SPO to SQL update completed.");
    }

    public static void RefreshListsSQL(string listName, string ctxURL, SPOUser user, bool daily, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Logger.Log(1, $"RefreshListsSQL: Initializing SPO list for: {listName}");

            SPOList spoList = null;
            try
            {
                spoList = new SPOList
                {
                    Site = ctxURL,
                    SPOUser = user,
                    Name = listName
                };

                Logger.Log(1, "RefreshListsSQL: SPOList initialized:");
                Logger.Log(1, $"  Site: {spoList.Site}");
                Logger.Log(1, $"  Name: {spoList.Name}");
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
                sqlInteraction = new SQLInteraction
                {
                    List = spoList,
                    DailyMode = daily,
                    CommandTimeoutSeconds = 300
                };

                cancellationToken.ThrowIfCancellationRequested();

                Logger.Log(1, "RefreshListsSQL: Building SQL interaction...");
                sqlInteraction.Build();

                if (daily)
                {
                    Logger.Log(2, "RefreshListsSQL: Performing daily update...");
                    sqlInteraction.DailyUpdate();
                }
                else
                {
                    Logger.Log(2, "RefreshListsSQL: Performing current time update...");
                    sqlInteraction.CurrentTimeUpdate();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RefreshListsSQL: ERROR - Failed to build SQLInteraction for '{listName}'.");
                Console.WriteLine("Exception: " + ex.Message);
                Console.WriteLine("Stack Trace: " + ex.StackTrace);
                return;
            }
            finally
            {
                sqlInteraction?.Dispose();
            }

            Logger.Log(2, $"RefreshListsSQL: Update for list '{listName}' completed.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"RefreshListsSQL: FATAL ERROR updating list '{listName}': {ex.Message}");
            Console.WriteLine("Stack Trace: " + ex.StackTrace);
        }
    }
}
