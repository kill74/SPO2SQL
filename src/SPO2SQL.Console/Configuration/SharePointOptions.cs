using System.ComponentModel.DataAnnotations;

namespace SPO2SQL.Configuration;

public record SharePointOptions
{
    public const string SectionName = "SharePoint";

    [Required(ErrorMessage = "SharePoint username is required")]
    [EmailAddress(ErrorMessage = "SharePoint username must be a valid email address")]
    public string Username { get; init; } = string.Empty;

    [Required(ErrorMessage = "SharePoint password is required")]
    [MinLength(1, ErrorMessage = "SharePoint password cannot be empty")]
    public string Password { get; init; } = string.Empty;

    [Required(ErrorMessage = "SharePoint site URL is required")]
    [Url(ErrorMessage = "SharePoint site URL must be a valid URL")]
    public string SiteUrl { get; init; } = string.Empty;

    [Range(10, 600, ErrorMessage = "Timeout must be between 10 and 600 seconds")]
    public int TimeoutSeconds { get; init; } = 120;

    [Range(0, 10, ErrorMessage = "Max retries must be between 0 and 10")]
    public int MaxRetries { get; init; } = 3;

    [Range(100, 10000, ErrorMessage = "Initial retry delay must be between 100 and 10000 ms")]
    public int InitialRetryDelayMs { get; init; } = 1000;
}
