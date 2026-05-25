using FluentAssertions;
using SPO2SQL.Configuration;
using Xunit;

namespace SPO2SQL.Console.Tests;

public class ApplicationOptionsTests
{
    [Fact]
    public void ApplicationOptions_DefaultValues_AreSet()
    {
        var options = new ApplicationOptions();

        options.Name.Should().Be("SharePoint Sync Tool");
        options.Version.Should().Be("2.0.0");
        options.Environment.Should().Be("Production");
        options.EnableMetrics.Should().BeTrue();
        options.EnableHealthChecks.Should().BeTrue();
    }
}

public class SharePointOptionsTests
{
    [Fact]
    public void SharePointOptions_DefaultValues_AreSet()
    {
        var options = new SharePointOptions();

        options.TimeoutSeconds.Should().Be(120);
        options.MaxRetries.Should().Be(3);
        options.InitialRetryDelayMs.Should().Be(1000);
        options.Username.Should().BeEmpty();
        options.Password.Should().BeEmpty();
        options.SiteUrl.Should().BeEmpty();
    }
}

public class SqlOptionsTests
{
    [Fact]
    public void SqlOptions_DefaultValues_AreSet()
    {
        var options = new SqlOptions();

        options.CommandTimeoutSeconds.Should().Be(300);
        options.BatchSize.Should().Be(80);
        options.EnforceEncryption.Should().BeTrue();
        options.ConnectionString.Should().BeEmpty();
    }
}
