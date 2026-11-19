using System.Diagnostics;

namespace SPO2SQL.Telemetry;

/// <summary>
/// Activity source used to trace list synchronization, SharePoint reads and SQL writes.
/// The host subscribes to it when metrics/telemetry are enabled.
/// </summary>
public static class SyncTracing
{
    public const string SourceName = "SPO2SQL";

    public static readonly ActivitySource Source = new(SourceName, "2.0.0");

    public const string ListTag = "spo2sql.list";
    public const string ModeTag = "spo2sql.mode";
    public const string RowsTag = "spo2sql.rows";
}
