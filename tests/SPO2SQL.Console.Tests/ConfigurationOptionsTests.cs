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
        options.MaxParallelLists.Should().Be(4);
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

    [Fact]
    public void Validate_WithUsernameAndPassword_UsesLegacyAuthentication()
    {
        var options = new SharePointOptions
        {
            SiteUrl = "https://contoso.sharepoint.com",
            Username = "user@contoso.com",
            Password = "secret"
        };

        Validate(options).Should().BeEmpty();
        options.HasAppOnlyAuth.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithHttpSiteUrl_ReturnsError()
    {
        var options = new SharePointOptions
        {
            SiteUrl = "http://contoso.sharepoint.com",
            Username = "user@contoso.com",
            Password = "secret"
        };

        Validate(options).Should().Contain(result =>
            result.ErrorMessage == "SharePoint site URL must use HTTPS.");
    }

    [Fact]
    public void Validate_WithoutAnyAuthenticationMode_ReturnsError()
    {
        var options = new SharePointOptions
        {
            SiteUrl = "https://contoso.sharepoint.com"
        };

        Validate(options).Should().NotBeEmpty();
    }

    [Fact]
    public void Validate_WithCompleteAppOnlySettings_AcceptsExistingCertificate()
    {
        string certificatePath = Path.GetTempFileName();
        try
        {
            var options = new SharePointOptions
            {
                SiteUrl = "https://contoso.sharepoint.com",
                TenantId = "tenant-id",
                ClientId = "client-id",
                CertificatePath = certificatePath
            };

            Validate(options).Should().BeEmpty();
            options.HasAppOnlyAuth.Should().BeTrue();
        }
        finally
        {
            File.Delete(certificatePath);
        }
    }

    [Fact]
    public void Validate_WithMissingCertificate_ReturnsError()
    {
        var options = new SharePointOptions
        {
            SiteUrl = "https://contoso.sharepoint.com",
            TenantId = "tenant-id",
            ClientId = "client-id",
            CertificatePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pfx")
        };

        Validate(options).Should().NotBeEmpty();
    }

    private static List<System.ComponentModel.DataAnnotations.ValidationResult> Validate(SharePointOptions options)
    {
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            options,
            new System.ComponentModel.DataAnnotations.ValidationContext(options),
            results,
            validateAllProperties: true);
        return results;
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
