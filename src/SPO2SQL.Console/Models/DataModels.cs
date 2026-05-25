namespace SPO2SQL.Models;

public record SharePointListItem
{
    public int Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public DateTime Created { get; init; }

    public DateTime? Modified { get; init; }

    public string? Author { get; init; }

    public Dictionary<string, object?> CustomFields { get; init; } = new();
}

public record SyncOperation(
    Guid OperationId,
    SyncType Type,
    DateTime StartTime,
    DateTime? EndTime,
    int ItemsProcessed,
    SyncStatus Status
)
{
    public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;

    public bool IsRunning => EndTime is null && Status == SyncStatus.Running;
}

public enum SyncType
{
    Daily,
    Monthly,
    Manual
}

public enum SyncStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}

public record DataQualityIssue(
    string ListName,
    int ItemId,
    string FieldName,
    string IssueType,
    string Description,
    Severity Severity
)
{
    public string ErrorMessage =>
        $"[{Severity}] {ListName}[{ItemId}].{FieldName}: {IssueType} - {Description}";

    public bool ShouldBlockSync => Severity is Severity.Critical;
}

public enum Severity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public record SyncStatistics(
    int TotalItems,
    int Successful,
    int Failed,
    TimeSpan Duration
)
{
    public double SuccessRate => TotalItems > 0
        ? (Successful / (double)TotalItems) * 100
        : 0;

    public double FailureRate => TotalItems > 0
        ? (Failed / (double)TotalItems) * 100
        : 0;

    public double Throughput => Duration.TotalSeconds > 0
        ? Successful / Duration.TotalSeconds
        : 0;

    public TimeSpan AverageTimePerItem => Successful > 0
        ? TimeSpan.FromTicks(Duration.Ticks / Successful)
        : TimeSpan.Zero;

    public bool MeetsQualityStandards => SuccessRate >= 95.0;

    public string Summary =>
        $"{Successful}/{TotalItems} items synced ({SuccessRate:F1}%) in {Duration.TotalSeconds:F1}s @ {Throughput:F2} items/sec";
}

public record ConnectionInfo(
    string SiteName,
    string DatabaseName,
    string Environment,
    DateTime? LastSync
)
{
    public bool IsProduction => Environment.Equals("Production", StringComparison.OrdinalIgnoreCase);

    public TimeSpan? TimeSinceLastSync => LastSync.HasValue
        ? DateTime.UtcNow - LastSync.Value
        : null;

    public string Description =>
        $"{SiteName} \u2192 {DatabaseName} ({Environment})";
}
