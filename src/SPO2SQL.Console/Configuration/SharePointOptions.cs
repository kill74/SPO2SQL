using System.ComponentModel.DataAnnotations;

namespace SPO2SQL.Configuration;

public record SharePointOptions : IValidatableObject
{
    public const string SectionName = "SharePoint";

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    [Required(ErrorMessage = "SharePoint site URL is required")]
    [Url(ErrorMessage = "SharePoint site URL must be a valid URL")]
    public string SiteUrl { get; init; } = string.Empty;

    [Range(10, 600, ErrorMessage = "Timeout must be between 10 and 600 seconds")]
    public int TimeoutSeconds { get; init; } = 120;

    [Range(1, 10, ErrorMessage = "Max retries must be between 1 and 10")]
    public int MaxRetries { get; init; } = 3;

    [Range(100, 10000, ErrorMessage = "Initial retry delay must be between 100 and 10000 ms")]
    public int InitialRetryDelayMs { get; init; } = 1000;

    // App-only authentication. When configured, these take precedence over
    // username/password and work with MFA and Conditional Access policies.
    public string? TenantId { get; init; }

    public string? ClientId { get; init; }

    public string? CertificatePath { get; init; }

    public string? CertificatePassword { get; init; }

    public bool HasAppOnlyAuth =>
        !string.IsNullOrWhiteSpace(TenantId) &&
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(CertificatePath);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        bool hasUserPassword =
            !string.IsNullOrWhiteSpace(Username) &&
            !string.IsNullOrWhiteSpace(Password);

        if (Uri.TryCreate(SiteUrl, UriKind.Absolute, out var siteUri) &&
            siteUri.Scheme != Uri.UriSchemeHttps)
        {
            yield return new ValidationResult(
                "SharePoint site URL must use HTTPS.",
                [nameof(SiteUrl)]);
        }

        if (!string.IsNullOrWhiteSpace(Username) &&
            !new EmailAddressAttribute().IsValid(Username))
        {
            yield return new ValidationResult(
                "SharePoint username must be a valid email address.",
                [nameof(Username)]);
        }

        if (!hasUserPassword && !HasAppOnlyAuth)
        {
            yield return new ValidationResult(
                "Configure either Username/Password or app-only authentication " +
                "(TenantId, ClientId and CertificatePath).",
                [nameof(Username), nameof(TenantId), nameof(ClientId), nameof(CertificatePath)]);
        }

        if (HasAppOnlyAuth && !File.Exists(CertificatePath))
        {
            yield return new ValidationResult(
                $"The configured SharePoint certificate was not found: '{CertificatePath}'.",
                [nameof(CertificatePath)]);
        }
    }
}
