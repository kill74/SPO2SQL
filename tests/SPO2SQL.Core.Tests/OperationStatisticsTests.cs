using FluentAssertions;
using SPO2SQL.SharePoint;
using Xunit;

namespace SPO2SQL.Core.Tests;

public class OperationStatisticsTests
{
    [Fact]
    public void SuccessRate_WithAllSuccessful_Returns100()
    {
        var stats = new OperationStatistics
        {
            TotalItemsProcessed = 100,
            SuccessfulUpdates = 100
        };

        stats.SuccessRate.Should().Be(100.0);
    }

    [Fact]
    public void SuccessRate_WithHalfFailed_Returns50()
    {
        var stats = new OperationStatistics
        {
            TotalItemsProcessed = 100,
            SuccessfulUpdates = 50,
            FailedUpdates = 50
        };

        stats.SuccessRate.Should().Be(50.0);
    }

    [Fact]
    public void SuccessRate_WithNoItems_ReturnsZero()
    {
        var stats = new OperationStatistics();

        stats.SuccessRate.Should().Be(0);
    }

    [Fact]
    public void Duration_WithValidRange_ReturnsDifference()
    {
        var stats = new OperationStatistics
        {
            StartTime = new DateTime(2024, 1, 1, 10, 0, 0),
            EndTime = new DateTime(2024, 1, 1, 10, 30, 0)
        };

        stats.Duration.TotalMinutes.Should().Be(30.0);
    }

    [Fact]
    public void AverageTimePerItem_WithItems_ReturnsCorrectAverage()
    {
        var stats = new OperationStatistics
        {
            TotalItemsProcessed = 100,
            StartTime = new DateTime(2024, 1, 1, 10, 0, 0),
            EndTime = new DateTime(2024, 1, 1, 10, 1, 0)
        };

        stats.AverageTimePerItem.Should().BeApproximately(600.0, 0.01);
    }

    [Fact]
    public void ToString_ReturnsFormattedSummary()
    {
        var stats = new OperationStatistics
        {
            TotalItemsProcessed = 200,
            SuccessfulUpdates = 180,
            FailedUpdates = 15,
            SkippedItems = 5
        };

        var result = stats.ToString();

        result.Should().Contain("200 processed")
              .And.Contain("180 succeeded")
              .And.Contain("15 failed")
              .And.Contain("5 skipped");
    }
}
