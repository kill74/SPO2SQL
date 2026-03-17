using System;

namespace Bring.Sharepoint
{
  /// <summary>
  /// Provides context and tracking information for data quality operations.
  /// Includes correlation ID for tracing and statistics collection.
  /// </summary>
  public class OperationContext
  {
    /// <summary>
    /// Unique identifier for correlating operation logs across the system.
    /// Useful for debugging and tracing related operations.
    /// </summary>
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 8);

    /// <summary>
    /// Operation name for identification in logs.
    /// </summary>
    public string OperationName { get; set; }

    /// <summary>
    /// Timestamp when the operation context was created.
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.Now;

    /// <summary>
    /// Timestamp when the operation completed (set at finish).
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Detailed statistics about the operation execution.
    /// </summary>
    public OperationStatistics Statistics { get; set; } = new OperationStatistics();

    /// <summary>
    /// Indicates whether the operation is currently in progress.
    /// </summary>
    public bool IsInProgress => EndTime == null;

    /// <summary>
    /// Gets the total duration if the operation has completed.
    /// </summary>
    public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;

    /// <summary>
    /// Marks the operation as completed.
    /// </summary>
    public void MarkComplete()
    {
      EndTime = DateTime.Now;
      Statistics.EndTime = EndTime.Value;
      Logger.LogWarning($"[{CorrelationId}] Operation '{OperationName}' completed: {Statistics}");
    }

    /// <summary>
    /// Gets a summary string for the operation context.
    /// </summary>
    public override string ToString()
    {
      return $"OperationContext [ID: {CorrelationId}, Name: {OperationName}, " +
             $"InProgress: {IsInProgress}, Duration: {Duration?.TotalSeconds:F2}s]";
    }
  }
}
