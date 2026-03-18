using System.ComponentModel.DataAnnotations;

namespace Bring.Configuration;

/// <summary>
/// Configuration options for SharePoint Online connectivity.
/// </summary>
public record SharePointOptions
{
    public const string SectionName = "SharePoint";

    /// <summary>
    /// SharePoint Online username (email format).
    /// </summary>
    [Required(ErrorMessage = "SharePoint username is required")]
    [EmailAddress(ErrorMessage = "SharePoint username must be a valid email address")]
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// SharePoint Online password.
    /// Note: For security, use User Secrets (development) or Environment Variables (production).
    /// </summary>
    [Required(ErrorMessage = "SharePoint password is required")]
    [MinLength(1, ErrorMessage = "SharePoint password cannot be empty")]
    public string Password { get; init; } = string.Empty;

    /// <summary>
    /// Base SharePoint site URL.
    /// </summary>
    [Required(ErrorMessage = "SharePoint site URL is required")]
    [Url(ErrorMessage = "SharePoint site URL must be a valid URL")]
    public string SiteUrl { get; init; } = string.Empty;

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    [Range(10, 600, ErrorMessage = "Timeout must be between 10 and 600 seconds")]
    public int TimeoutSeconds { get; init; } = 120;

    /// <summary>
    /// Maximum retry attempts for transient failures.
    /// </summary>
    [Range(0, 10, ErrorMessage = "Max retries must be between 0 and 10")]
    public int MaxRetries { get; init} = 3;

    /// <summary>
    /// Initial retry delay in milliseconds.
    /// </summary>
    [Range(100, 10000, ErrorMessage = "Initial retry delay must be between 100 and 10000 ms")]
    public int InitialRetryDelayMs { get; init; } = 1000;
}
