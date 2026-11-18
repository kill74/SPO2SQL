using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SPO2SQL.Logging;
using Xunit;

// CA1873 targets production logging calls; the calls in this file are NSubstitute
// verifications whose argument matchers must be evaluated eagerly.
#pragma warning disable CA1873

namespace SPO2SQL.Core.Tests;

[CollectionDefinition("LoggingTests", DisableParallelization = true)]
public class LoggingTestDefinition
{
}

[Collection("LoggingTests")]
public class LoggerTests
{
    [Fact]
    public void Configure_WithNullSink_ThrowsArgumentNullException()
    {
        var act = () => Logger.Configure(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void LegacyLogLevels_AreRoutedToTheHostLogger()
    {
        var sink = Substitute.For<ILogger>();
        sink.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        Logger.Configure(sink);

        try
        {
            Logger.Log(1, "legacy-info");
            Logger.LogWarning("legacy-warning");
            Logger.LogDebug("legacy-debug");
            Logger.LogError("legacy-error", new InvalidOperationException("boom"));

            AssertLogged(sink, LogLevel.Information, "legacy-info");
            AssertLogged(sink, LogLevel.Warning, "legacy-warning");
            AssertLogged(sink, LogLevel.Debug, "legacy-debug");
            sink.Received(1).Log<string>(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Is<string>(s => s == "legacy-error"),
                Arg.Is<Exception>(e => e is InvalidOperationException),
                Arg.Any<Func<string, Exception?, string>>());
        }
        finally
        {
            Logger.Reset();
        }
    }

    [Fact]
    public void VerboseLevel_FiltersDebugMessages()
    {
        var sink = Substitute.For<ILogger>();
        sink.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        Logger.Configure(sink);
        Logger.VerboseLevel = 1;

        try
        {
            Logger.Log(3, "hidden-debug");

            sink.DidNotReceive().Log<string>(
                LogLevel.Debug,
                Arg.Any<EventId>(),
                Arg.Is<string>(s => s == "hidden-debug"),
                (Exception?)null,
                Arg.Any<Func<string, Exception?, string>>());
        }
        finally
        {
            Logger.Reset();
        }
    }

    private static void AssertLogged(ILogger sink, LogLevel level, string message, Exception? exception = null) =>
        sink.Received(1).Log<string>(
            level,
            Arg.Any<EventId>(),
            Arg.Is<string>(s => s == message),
            exception,
            Arg.Any<Func<string, Exception?, string>>());
}
