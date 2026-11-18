using System.Diagnostics;
using FluentAssertions;
using SPO2SQL.Telemetry;
using Xunit;

namespace SPO2SQL.Core.Tests;

public class SyncTracingTests
{
    [Fact]
    public void Source_EmitsActivitiesToListeners()
    {
        Activity? captured = null;

        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == SyncTracing.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => captured = activity
        };

        ActivitySource.AddActivityListener(listener);

        using var activity = SyncTracing.Source.StartActivity("spo2sql.sync.list", ActivityKind.Internal);
        activity?.SetTag(SyncTracing.ListTag, "Activities");

        captured.Should().NotBeNull();
        captured!.OperationName.Should().Be("spo2sql.sync.list");
        captured.GetTagItem(SyncTracing.ListTag).Should().Be("Activities");
    }
}
