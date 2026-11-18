using FluentAssertions;
using SPO2SQL.Configuration;
using Xunit;

namespace SPO2SQL.Core.Tests;

public class SyncSettingsTests
{
    [Fact]
    public void HasAppOnlyAuth_WithCompleteAppOnlySettings_ReturnsTrue()
    {
        var settings = new SyncSettings
        {
            SiteUrl = "https://contoso.sharepoint.com",
            SqlConnectionString = "Server=.;Database=x;",
            TenantId = "tenant-id",
            ClientId = "client-id",
            CertificatePath = "/certs/app.pfx"
        };

        settings.HasAppOnlyAuth.Should().BeTrue();
    }

    [Fact]
    public void HasAppOnlyAuth_WithIncompleteSettings_ReturnsFalse()
    {
        var settings = new SyncSettings
        {
            SiteUrl = "https://contoso.sharepoint.com",
            SqlConnectionString = "Server=.;Database=x;",
            TenantId = "tenant-id",
            ClientId = "client-id"
        };

        settings.HasAppOnlyAuth.Should().BeFalse();
    }

    [Fact]
    public void HasAppOnlyAuth_WithOnlyUserPassword_ReturnsFalse()
    {
        var settings = new SyncSettings
        {
            SiteUrl = "https://contoso.sharepoint.com",
            SqlConnectionString = "Server=.;Database=x;",
            Username = "user@contoso.com",
            Password = "secret"
        };

        settings.HasAppOnlyAuth.Should().BeFalse();
    }
}
