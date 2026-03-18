namespace Bring.Models;

/// <summary>
/// Represents a generic SharePoint list item with common metadata.
/// </summary>
/// <remarks>
/// <para>Uses init-only properties for immutability while allowing object initializers.</para>
/// <para><strong>With-expressions for non-destructive mutation:</strong></para>
/// <code>
/// var original = new SharePointListItem 
/// { 
///     Id = 1, 
///     Title = "Initial Title", 
///     Created = DateTime.UtcNow 
/// };
/// 
/// // Create a modified copy with updated title
/// var updated = original with { Title = "Updated Title" };
/// 
/// // Chain multiple with-expressions
/// var furtherUpdated = updated with 
/// { 
///     Modified = DateTime.UtcNow,
///     Author = "admin@contoso.com" 
/// };
/// 
/// // Original remains unchanged (immutability)
/// Console.WriteLine(original.Title); // "Initial Title"
/// Console.WriteLine(updated.Title);  // "Updated Title"
/// </code>
/// </remarks>
public record SharePointListItem
{
    /// <summary>
    /// The unique identifier of the list item.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// The title/display name of the list item.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// The timestamp when the item was created in SharePoint.
    /// </summary>
    public DateTime Created { get; init; }

    /// <summary>
    /// The timestamp when the item was last modified in SharePoint.
    /// </summary>
    public DateTime? Modified { get; init; }

    /// <summary>
    /// The user principal name (email) of the item author.
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    /// Additional custom fields stored as key-value pairs.
    /// </summary>
    public Dictionary<string, object?>? CustomFields { get; init; }
}

/// <summary>
/// Represents a sync operation execution with timing and metrics.
/// Uses positional record syntax for concise declaration.
/// </summary>
/// <remarks>
/// <para>Positional records provide automatic properties, constructor, and deconstruction:</para>
/// <code>
/// var op = new SyncOperation(
///     Guid.NewGuid(), 
///     SyncType.Daily, 
///     DateTime.UtcNow, 
///     null, 
///     0, 
///     "Running"
/// );
/// 
/// // Deconstruction
/// var (opId, type, start, _, _, status) = op;
/// 
/// // With-expression for updates
/// var completed = op with 
/// { 
///     EndTime = DateTime.UtcNow, 
///     ItemsProcessed = 1500,
///     Status = "Completed" 
/// };
/// </code>
/// </remarks>
/// <param name="OperationId">Unique identifier for the sync operation.</param>
/// <param name="Type">Type of sync operation (Daily or Monthly).</param>
/// <param name="StartTime">When the operation started.</param>
/// <param name="EndTime">When the operation completed (null if still running).</param>
/// <param name="ItemsProcessed">Number of items processed during the operation.</param>
/// <param name="Status">Current status of the operation.</param>
public record SyncOperation(
    Guid OperationId,
    SyncType Type,
    DateTime StartTime,
    DateTime? EndTime,
    int ItemsProcessed,
    string Status
)
{
    /// <summary>
    /// Gets the duration of the operation. Returns null if not yet completed.
    /// </summary>
    public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;

    /// <summary>
    /// Gets whether the operation is currently running.
    /// </summary>
    public bool IsRunning => EndTime is null && Status == "Running";
}

/// <summary>
/// Type of synchronization operation.
/// </summary>
public enum SyncType
{
    /// <summary>Daily incremental sync.</summary>
    Daily,
    /// <summary>Monthly full sync.</summary>
    Monthly,
    /// <summary>Manual on-demand sync.</summary>
    Manual
}

/// <summary>
/// Represents a data quality issue found during sync operations.
/// Demonstrates pattern matching capabilities with records.
/// </summary>
/// <remarks>
/// <para><strong>Pattern matching examples:</strong></para>
/// <code>
/// var issue = new DataQualityIssue(
///     "CustomerList",
///     123,
///     "Email",
///     "InvalidFormat",
///     "Email address missing @ symbol",
///     Severity.High
/// );
/// 
/// // Property pattern matching
/// var severity = issue switch
/// {
///     { Severity: Severity.Critical } => "URGENT",
///     { Severity: Severity.High, IssueType: "InvalidFormat" } => "Fix Format",
///     { Severity: Severity.Medium } => "Review",
///     _ => "Log Only"
/// };
/// 
/// // Positional pattern matching
/// var action = issue switch
/// {
///     ("CustomerList", _, "Email", _, _, Severity.Critical) => "Block sync",
///     (_, _, _, "MissingRequired", _, _) => "Skip item",
///     _ => "Continue"
/// };
/// 
/// // Relational pattern
/// var priority = issue switch
/// {
///     { Severity: >= Severity.High } => 1,
///     { Severity: Severity.Medium } => 2,
///     _ => 3
/// };
/// </code>
/// </remarks>
/// <param name="ListName">Name of the SharePoint list where issue was found.</param>
/// <param name="ItemId">ID of the affected list item.</param>
/// <param name="FieldName">Name of the field with the issue.</param>
/// <param name="IssueType">Type/category of the data quality issue.</param>
/// <param name="Description">Detailed description of the issue.</param>
/// <param name="Severity">Severity level of the issue.</param>
public record DataQualityIssue(
    string ListName,
    int ItemId,
    string FieldName,
    string IssueType,
    string Description,
    Severity Severity
)
{
    /// <summary>
    /// Gets a formatted error message for logging.
    /// </summary>
    public string ErrorMessage => 
        $"[{Severity}] {ListName}[{ItemId}].{FieldName}: {IssueType} - {Description}";

    /// <summary>
    /// Determines if this issue should block sync operations.
    /// </summary>
    public bool ShouldBlockSync => Severity is Severity.Critical;
}

/// <summary>
/// Severity level for data quality issues.
/// </summary>
public enum Severity
{
    /// <summary>Informational only.</summary>
    Low = 1,
    /// <summary>Should be reviewed.</summary>
    Medium = 2,
    /// <summary>Needs attention.</summary>
    High = 3,
    /// <summary>Blocks operations.</summary>
    Critical = 4
}

/// <summary>
/// Aggregated statistics for a sync operation.
/// Demonstrates calculated properties using expression body syntax.
/// </summary>
/// <param name="TotalItems">Total number of items in the sync scope.</param>
/// <param name="Successful">Number of items successfully synced.</param>
/// <param name="Failed">Number of items that failed to sync.</param>
/// <param name="Duration">Total duration of the sync operation.</param>
public record SyncStatistics(
    int TotalItems,
    int Successful,
    int Failed,
    TimeSpan Duration
)
{
    /// <summary>
    /// Gets the success rate as a percentage (0-100).
    /// </summary>
    public double SuccessRate => TotalItems > 0 
        ? (Successful / (double)TotalItems) * 100 
        : 0;

    /// <summary>
    /// Gets the failure rate as a percentage (0-100).
    /// </summary>
    public double FailureRate => TotalItems > 0 
        ? (Failed / (double)TotalItems) * 100 
        : 0;

    /// <summary>
    /// Gets the throughput in items per second.
    /// </summary>
    public double Throughput => Duration.TotalSeconds > 0 
        ? Successful / Duration.TotalSeconds 
        : 0;

    /// <summary>
    /// Gets the average processing time per item.
    /// </summary>
    public TimeSpan AverageTimePerItem => Successful > 0 
        ? TimeSpan.FromTicks(Duration.Ticks / Successful) 
        : TimeSpan.Zero;

    /// <summary>
    /// Gets whether the sync operation met quality standards (>95% success rate).
    /// </summary>
    public bool MeetsQualityStandards => SuccessRate >= 95.0;

    /// <summary>
    /// Gets a human-readable summary of the statistics.
    /// </summary>
    public string Summary => 
        $"{Successful}/{TotalItems} items synced ({SuccessRate:F1}%) in {Duration.TotalSeconds:F1}s @ {Throughput:F2} items/sec";
}

/// <summary>
/// Contains connection information for SharePoint and SQL database.
/// Demonstrates value-based equality behavior of records.
/// </summary>
/// <remarks>
/// <para><strong>Value equality demonstration:</strong></para>
/// <code>
/// var conn1 = new ConnectionInfo(
///     "portal.contoso.com",
///     "SharePointDB",
///     "Production",
///     DateTime.Parse("2024-01-15T10:30:00Z")
/// );
/// 
/// var conn2 = new ConnectionInfo(
///     "portal.contoso.com",
///     "SharePointDB",
///     "Production",
///     DateTime.Parse("2024-01-15T10:30:00Z")
/// );
/// 
/// // Reference equality (different objects)
/// Console.WriteLine(ReferenceEquals(conn1, conn2)); // False
/// 
/// // Value equality (same property values)
/// Console.WriteLine(conn1 == conn2);  // True
/// Console.WriteLine(conn1.Equals(conn2)); // True
/// Console.WriteLine(conn1.GetHashCode() == conn2.GetHashCode()); // True
/// 
/// // Useful for collections
/// var connections = new HashSet&lt;ConnectionInfo&gt; { conn1 };
/// connections.Add(conn2); // Won't add duplicate due to value equality
/// Console.WriteLine(connections.Count); // 1
/// 
/// // Modified copy breaks equality
/// var conn3 = conn1 with { Environment = "Development" };
/// Console.WriteLine(conn1 == conn3); // False
/// </code>
/// </remarks>
/// <param name="SiteName">SharePoint site name or URL.</param>
/// <param name="DatabaseName">SQL Server database name.</param>
/// <param name="Environment">Environment identifier (Development, Staging, Production).</param>
/// <param name="LastSync">Timestamp of the last successful sync operation.</param>
public record ConnectionInfo(
    string SiteName,
    string DatabaseName,
    string Environment,
    DateTime? LastSync
)
{
    /// <summary>
    /// Gets whether the connection is to a production environment.
    /// </summary>
    public bool IsProduction => Environment.Equals("Production", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the time elapsed since the last sync.
    /// </summary>
    public TimeSpan? TimeSinceLastSync => LastSync.HasValue 
        ? DateTime.UtcNow - LastSync.Value 
        : null;

    /// <summary>
    /// Gets a formatted connection description.
    /// </summary>
    public string Description => 
        $"{SiteName} → {DatabaseName} ({Environment})";
}
