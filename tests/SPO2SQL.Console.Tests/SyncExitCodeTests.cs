using FluentAssertions;
using Xunit;

namespace SPO2SQL.Console.Tests;

public class SyncExitCodeTests
{
    [Fact]
    public void NewInstance_DefaultsToSuccess()
    {
        new SyncExitCode().Value.Should().Be(0);
    }

    [Fact]
    public void Fail_SetsExitCodeToOne()
    {
        var exitCode = new SyncExitCode();

        exitCode.Fail();

        exitCode.Value.Should().Be(1);
    }

    [Fact]
    public void Fail_IsIdempotent()
    {
        var exitCode = new SyncExitCode();

        exitCode.Fail();
        exitCode.Fail();

        exitCode.Value.Should().Be(1);
    }
}
