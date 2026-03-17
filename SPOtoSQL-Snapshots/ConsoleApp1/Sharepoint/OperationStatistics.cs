using System;

namespace Bring.Sharepoint
{
  /// <summary>
  /// Tracks metrics and statistics for data quality operations.
  /// Provides detailed information about processing success rates, timing, and item counts.
  /// </summary>
  public class OperationStatistics
  {
    /// <summary>
    /// Total number of items processed during the operation.
    /// </summary>
    public int TotalItemsProcessed { get; set; }

    /// <summary>
    /// Number of items successfully updated.
    /// </summary>
    public int SuccessfulUpdates { get; set; }

    /// <summary>
    /// Number of items that failed to update.
    /// </summary>
    public int FailedUpdates { get; set; }

    /// <summary>
    /// Number of items skipped (no action needed).
    /// </summary>
    public int SkippedItems { get; set; }

    /// <summary>
    /// Timestamp when the operation started.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Timestamp when the operation completed.
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Total duration of the operation.
    /// </summary>
    public TimeSpan Duration => EndTime > StartTime ? EndTime - StartTime : TimeSpan.Zero;

    /// <summary>
    /// Success rate as a percentage (0-100).
    /// </summary>
    public double SuccessRate
    {
      get
      {
        if (TotalItemsProcessed == 0) return 0;
        return (SuccessfulUpdates / (double)TotalItemsProcessed) * 100;
      }
    }

    /// <summary>
    /// Average time per item in milliseconds.
    /// </summary>
    public double AverageTimePerItem
    {
      get
      {
        if (TotalItemsProcessed == 0) return 0;
        return Duration.TotalMilliseconds / TotalItemsProcessed;
      }
    }

    /// <summary>
    /// Gets a human-readable summary of the operation statistics.
    /// </summary>
    public override string ToString()
    {
      return $"Statistics: {TotalItemsProcessed} processed, " +
             $"{SuccessfulUpdates} succeeded, {FailedUpdates} failed, " +
             $"{SkippedItems} skipped | " +
             $"Duration: {Duration.TotalSeconds:F2}s | " +
             $"Success Rate: {SuccessRate:F1}%";
    }
  }
}
