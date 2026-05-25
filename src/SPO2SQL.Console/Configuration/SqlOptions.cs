using System.ComponentModel.DataAnnotations;

namespace SPO2SQL.Configuration;

public record SqlOptions
{
    public const string SectionName = "Sql";

    [Required(ErrorMessage = "SQL connection string is required")]
    [MinLength(10, ErrorMessage = "SQL connection string appears to be invalid")]
    public string ConnectionString { get; init; } = string.Empty;

    [Range(10, 3600, ErrorMessage = "Command timeout must be between 10 and 3600 seconds")]
    public int CommandTimeoutSeconds { get; init; } = 300;

    [Range(10, 1000, ErrorMessage = "Batch size must be between 10 and 1000")]
    public int BatchSize { get; init; } = 80;

    public bool EnforceEncryption { get; init; } = true;
}
