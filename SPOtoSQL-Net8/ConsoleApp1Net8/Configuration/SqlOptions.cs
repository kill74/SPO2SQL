using System.ComponentModel.DataAnnotations;

namespace Bring.Configuration;

/// <summary>
/// Configuration options for SQL Server connectivity.
/// </summary>
public record SqlOptions
{
    public const string SectionName = "Sql";

    /// <summary>
    /// SQL Server connection string.
    /// Note: For security, use User Secrets (development) or Environment Variables (production).
    /// </summary>
    [Required(ErrorMessage = "SQL connection string is required")]
    [MinLength(10, ErrorMessage = "SQL connection string appears to be invalid")]
    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>
    /// Command timeout in seconds.
    /// </summary>
    [Range(10, 3600, ErrorMessage = "Command timeout must be between 10 and 3600 seconds")]
    public int CommandTimeoutSeconds { get; init; } = 300;

    /// <summary>
    /// Batch size for bulk operations.
    /// </summary>
    [Range(10, 1000, ErrorMessage = "Batch size must be between 10 and 1000")]
    public int BatchSize { get; init; } = 80;

    /// <summary>
    /// Whether to enforce encrypted connections.
    /// </summary>
    public bool EnforceEncryption { get; init; } = true;
}
