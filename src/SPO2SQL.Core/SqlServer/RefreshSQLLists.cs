using SPO2SQL.Configuration;
using SPO2SQL.Logging;
using SPO2SQL.SharePoint;
using SPO2SQL.Telemetry;
using SPO2SQL.XmlConfig;

namespace SPO2SQL.SqlServer;

internal sealed class RefreshSQLLists
{
    public static async Task SPOtoSQLUpdateAsync(
        SyncSettings settings,
        bool daily,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Logger.Log(2, "SPOtoSQLUpdate: Starting SPO to SQL update. Daily: " + daily);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var user = SPOUserFactory.Create(settings);

            var listConfigs = ConfigurationReader.GetListConfigurations();
            var activeLists = listConfigs?
                .Where(kvp => !kvp.Value.Ignore)
                .ToArray() ?? [];

            int failedLists = 0;

            await Parallel.ForEachAsync(
                activeLists,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Max(1, settings.MaxParallelLists),
                    CancellationToken = cancellationToken
                },
                async (kvp, token) =>
                {
                    var listName = kvp.Key;
                    string ctxURL = kvp.Value.SharepointList;
                    Logger.Log(1, $"SPOtoSQLUpdate: Processing list: {listName} with URL: {ctxURL}");

                    try
                    {
                        await RefreshListsSQLAsync(listName, ctxURL, settings, user, daily, token)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        Logger.Log(2, $"SPOtoSQLUpdate: Cancelled while processing list '{listName}'.");
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref failedLists);
                        Logger.LogError($"SPOtoSQLUpdate: Exception while updating list '{listName}'.", ex);
                    }
                }).ConfigureAwait(false);

            if (failedLists > 0)
            {
                throw new InvalidOperationException(
                    $"Synchronization failed for {failedLists} configured list(s).");
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Log(2, "SPOtoSQLUpdate: Operation was cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError("SPOtoSQLUpdate: FATAL ERROR - Exception during SPO to SQL update process.", ex);
            throw;
        }

        Logger.Log(2, "SPOtoSQLUpdate: SPO to SQL update completed.");
    }

    public static async Task RefreshListsSQLAsync(
        string listName,
        string ctxURL,
        SyncSettings settings,
        SPOUser user,
        bool daily,
        CancellationToken cancellationToken = default)
    {
        var statistics = new OperationStatistics { StartTime = DateTime.UtcNow };

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Logger.Log(1, $"RefreshListsSQL: Initializing SPO list for: {listName}");

            using var spoList = new SPOList
            {
                Site = ctxURL,
                SPOUser = user,
                Name = listName,
                PageSize = settings.BatchSize
            };

            using var sqlInteraction = new SQLInteraction
            {
                List = spoList,
                DailyMode = daily,
                CommandTimeoutSeconds = settings.CommandTimeoutSeconds,
                BatchSize = settings.BatchSize,
                CancellationToken = cancellationToken
            };

            var retryPolicy = new RetryPolicy(settings.MaxRetries, settings.InitialRetryDelayMs);
            await retryPolicy.ExecuteWithRetryAsync(
                () => sqlInteraction.BuildAsync(),
                cancellationToken,
                $"Build SQL interaction for '{listName}'").ConfigureAwait(false);

            if (daily)
            {
                Logger.Log(2, "RefreshListsSQL: Performing daily update...");
                await sqlInteraction.DailyUpdateAsync().ConfigureAwait(false);
            }
            else
            {
                Logger.Log(2, "RefreshListsSQL: Performing current time update...");
                await sqlInteraction.CurrentTimeUpdateAsync().ConfigureAwait(false);
            }

            statistics.TotalItemsProcessed = spoList.ItemCollection.Count;
            statistics.SuccessfulUpdates = sqlInteraction.ProcessedItems;
            statistics.FailedUpdates = sqlInteraction.FailedItems;
            statistics.EndTime = DateTime.UtcNow;

            if (settings.EnableMetrics)
            {
                Logger.LogInformation($"[Metrics] List '{listName}': {statistics}");
                SyncMetrics.Record(statistics, listName, daily);
            }

            Logger.Log(2, $"RefreshListsSQL: Update for list '{listName}' completed.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError($"RefreshListsSQL: FATAL ERROR updating list '{listName}'.", ex);
            throw;
        }
    }
}
