using System.Diagnostics.Metrics;
using FluentAssertions;
using SPO2SQL.SharePoint;
using SPO2SQL.Telemetry;
using Xunit;

namespace SPO2SQL.Core.Tests;

public class SyncMetricsTests
{
    [Fact]
    public void Record_EmitsPerListCountersAndDuration()
    {
        var measurements = new List<(string Name, double Value)>();

        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == SyncMetrics.MeterName)
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            }
        };

        listener.SetMeasurementEventCallback<int>((instrument, measurement, _, _) =>
            measurements.Add((instrument.Name, measurement)));
        listener.SetMeasurementEventCallback<double>((instrument, measurement, _, _) =>
            measurements.Add((instrument.Name, measurement)));
        listener.Start();

        try
        {
            var statistics = new OperationStatistics
            {
                StartTime = DateTime.UtcNow.AddSeconds(-2),
                EndTime = DateTime.UtcNow,
                TotalItemsProcessed = 10,
                SuccessfulUpdates = 9,
                FailedUpdates = 1
            };

            SyncMetrics.Record(statistics, "Activities", incremental: true);
        }
        finally
        {
            listener.Dispose();
        }

        measurements.Should().Contain(x => x.Name == "spo2sql.lists.synced" && x.Value == 1);
        measurements.Should().Contain(x => x.Name == "spo2sql.items.processed" && x.Value == 10);
        measurements.Should().Contain(x => x.Name == "spo2sql.items.succeeded" && x.Value == 9);
        measurements.Should().Contain(x => x.Name == "spo2sql.items.failed" && x.Value == 1);
        measurements.Should().Contain(x => x.Name == "spo2sql.sync.duration" && x.Value > 0);
    }
}
