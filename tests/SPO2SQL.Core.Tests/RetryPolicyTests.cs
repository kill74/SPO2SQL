using FluentAssertions;
using SPO2SQL.SharePoint;
using Xunit;

namespace SPO2SQL.Core.Tests;

public class RetryPolicyTests
{
    [Fact]
    public void ExecuteWithRetry_SuccessfulOperation_ReturnsResult()
    {
        var policy = new RetryPolicy(maxRetries: 2, initialDelayMs: 100);

        var result = policy.ExecuteWithRetry(() => 42);

        result.Should().Be(42);
    }

    [Fact]
    public void ExecuteWithRetry_FailsAllAttempts_ThrowsLastException()
    {
        var policy = new RetryPolicy(maxRetries: 2, initialDelayMs: 100);
        var attempts = 0;

        var act = () => policy.ExecuteWithRetry(() =>
        {
            attempts++;
            throw new TimeoutException("Simulated transient error");
        });

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("Operation 'Unknown' failed after 3 attempts.")
           .Which.InnerException.Should().BeOfType<TimeoutException>()
           .Which.Message.Should().Be("Simulated transient error");
        attempts.Should().Be(3);
    }

    [Fact]
    public void ExecuteWithRetry_SucceedsOnRetry_ReturnsResult()
    {
        var policy = new RetryPolicy(maxRetries: 3, initialDelayMs: 100);
        var attempts = 0;

        var result = policy.ExecuteWithRetry(() =>
        {
            attempts++;
            if (attempts < 2)
            {
                throw new TimeoutException("Simulated transient error");
            }

            return 99;
        });

        result.Should().Be(99);
        attempts.Should().Be(2);
    }

    [Fact]
    public void IsTransientError_WithTimeoutException_ReturnsTrue()
    {
        var result = RetryPolicy.IsTransientError(new TimeoutException());

        result.Should().BeTrue();
    }

    [Fact]
    public void IsTransientError_WithNull_ReturnsFalse()
    {
        var result = RetryPolicy.IsTransientError(null);

        result.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithInvalidMaxRetries_ThrowsArgumentException()
    {
        var act = () => new RetryPolicy(maxRetries: 0);

        act.Should().Throw<ArgumentException>()
           .WithParameterName("maxRetries");
    }

    [Fact]
    public void Constructor_WithInvalidDelay_ThrowsArgumentException()
    {
        var act = () => new RetryPolicy(maxRetries: 3, initialDelayMs: 50);

        act.Should().Throw<ArgumentException>()
           .WithParameterName("initialDelayMs");
    }

    [Fact]
    public void ExecuteWithRetry_WithCancellation_ThrowsOperationCanceledException()
    {
        var policy = new RetryPolicy(maxRetries: 3, initialDelayMs: 100);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => policy.ExecuteWithRetry(() => 42, cts.Token);

        act.Should().Throw<OperationCanceledException>();
    }
}
