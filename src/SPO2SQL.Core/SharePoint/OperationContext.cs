using System;
using SPO2SQL.Logging;

namespace SPO2SQL.SharePoint;

public class OperationContext
{
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");

    public string OperationName { get; set; }

    public DateTime StartTime { get; set; } = DateTime.Now;

    public DateTime? EndTime { get; set; }

    public OperationStatistics Statistics { get; set; } = new OperationStatistics();

    public bool IsInProgress => EndTime == null;

    public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;

    public void MarkComplete()
    {
        EndTime = DateTime.Now;
        Statistics.EndTime = EndTime.Value;
        Logger.LogWarning($"[{CorrelationId}] Operation '{OperationName}' completed: {Statistics}");
    }

    public override string ToString()
    {
        return $"OperationContext [ID: {CorrelationId}, Name: {OperationName}, " +
               $"InProgress: {IsInProgress}, Duration: {Duration?.TotalSeconds:F2}s]";
    }
}
