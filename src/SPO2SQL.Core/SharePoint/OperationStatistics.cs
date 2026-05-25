using System;

namespace SPO2SQL.SharePoint;

public class OperationStatistics
{
    public int TotalItemsProcessed { get; set; }

    public int SuccessfulUpdates { get; set; }

    public int FailedUpdates { get; set; }

    public int SkippedItems { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public TimeSpan Duration => EndTime >= StartTime ? EndTime - StartTime : TimeSpan.Zero;

    public double SuccessRate
    {
        get
        {
            if (TotalItemsProcessed == 0)
            {
                return 0;
            }

            return (SuccessfulUpdates / (double)TotalItemsProcessed) * 100;
        }
    }

    public double AverageTimePerItem
    {
        get
        {
            if (TotalItemsProcessed == 0)
            {
                return 0;
            }

            return Duration.TotalMilliseconds / TotalItemsProcessed;
        }
    }

    public override string ToString()
    {
        return $"Statistics: {TotalItemsProcessed} processed, " +
               $"{SuccessfulUpdates} succeeded, {FailedUpdates} failed, " +
               $"{SkippedItems} skipped | " +
               $"Duration: {Duration.TotalSeconds:F2}s | " +
               $"Success Rate: {SuccessRate:F1}%";
    }
}
