using FluentAssertions;
using SPO2SQL.XmlConfig;
using Xunit;

namespace SPO2SQL.Core.Tests;

[CollectionDefinition("ConfigurationReaderTests", DisableParallelization = true)]
public class ConfigurationReaderTestDefinition
{
}

[Collection("ConfigurationReaderTests")]
public sealed class ConfigurationReaderTests : IDisposable
{
    private readonly string _configPath = Path.Combine(Path.GetTempPath(), $"spo2sql-{Guid.NewGuid():N}.xml");

    public ConfigurationReaderTests()
    {
        File.WriteAllText(_configPath, """
            <Configuration>
              <SharePoint>
                <BaseUrl>https://contoso.sharepoint.com</BaseUrl>
              </SharePoint>
              <ReplicationConfiguration>
                <SelectColumns>
                  <column source="Title" destination="ListItemTitle" />
                  <column source="Hidden" ignore="true" />
                </SelectColumns>
                <GlobalIgnore>
                  <Column>InternalField</Column>
                </GlobalIgnore>
                <Lists>
                  <List name="Activities" sharepointlist="wolf/activities" />
                  <List name="Ignored" sharepointlist="seed/ignored" ignore="true" />
                </Lists>
              </ReplicationConfiguration>
            </Configuration>
            """);

        ConfigurationReader.SetConfigPath(_configPath);
    }

    [Fact]
    public void GetSelectedColumns_UsesMappedDestinationAndIgnoreFlags()
    {
        var columns = ConfigurationReader.GetSelectedColumns("Activities");

        columns.Should().NotBeNull();
        columns!["Title"].Destination.Should().Be("ListItemTitle");
        columns["Hidden"].Ignore.Should().BeTrue();
    }

    [Fact]
    public void GetIgnoredColumns_ReturnsGlobalIgnores()
    {
        var columns = ConfigurationReader.GetIgnoredColumns();

        columns.Should().NotBeNull();
        columns!.Should().Contain("InternalField");
    }

    [Fact]
    public void GetListConfigurations_SkipsIgnoredLists()
    {
        var lists = ConfigurationReader.GetListConfigurations();

        lists.Should().NotBeNull();
        lists!["Activities"].Ignore.Should().BeFalse();
        lists["Ignored"].Ignore.Should().BeTrue();
        lists["Activities"].SharepointList.Should().Be("wolf/activities");
    }

    [Fact]
    public void GetSharePointBaseUrl_ReturnsConfiguredOverride()
    {
        ConfigurationReader.SetSharePointBaseUrl("https://override.sharepoint.com/sites/dev");

        ConfigurationReader.GetSharePointBaseUrl().Should().Be("https://override.sharepoint.com");
    }

    public void Dispose()
    {
        File.Delete(_configPath);
    }
}
