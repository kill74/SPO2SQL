using System.Diagnostics;
using System.Diagnostics.Metrics;
using SPO2SQL.SharePoint;

namespace SPO2SQL.Telemetry;

/// <summary>
/// OpenTelemetry instruments for the synchronization pipeline. The host subscribes
/// to the <see cref="MeterName"/> meter and exports it when metrics are enabled.
/// </summary>
public static class SyncMetrics
{
    public const string MeterName = "SPO2SQL";

    private static readonly Meter Meter = new(MeterName, "2.0.0");
    private static readonly Counter<int> ListsSynced =
        Meter.CreateCounter<int>("spo2sql.lists.synced", description: "Lists processed successfully");

    private static readonly Counter<int> ItemsProcessed =
        Meter.CreateCounter<int>("spo2sql.items.processed", unit: "items", description: "Items read from SharePoint");

    private static readonly Counter<int> ItemsSucceeded =
        Meter.CreateCounter<int>("spo2sql.items.succeeded", unit: "items", description: "Items written to SQL Server");

    private static readonly Counter<int> ItemsFailed =
        Meter.CreateCounter<int>("spo2sql.items.failed", unit: "items", description: "Items that failed to be written");

    private static readonly Histogram<double> SyncDuration =
        Meter.CreateHistogram<double>("spo2sql.sync.duration", unit: "s", description: "Duration of a list synchronization");

    public static void Record(OperationStatistics statistics, string listName, bool incremental)
    {
        var tags = new TagList
        {
            { "spo2sql.list", listName },
            { "spo2sql.mode", incremental ? "incremental" : "full" }
        };

        ListsSynced.Add(1, tags);
        ItemsProcessed.Add(statistics.TotalItemsProcessed, tags);
        ItemsSucceeded.Add(statistics.SuccessfulUpdates, tags);
        ItemsFailed.Add(statistics.FailedUpdates, tags);
        SyncDuration.Record(statistics.Duration.TotalSeconds, tags);
    }
}
