using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;

namespace SPO2SQL.Configuration;

public record ApplicationOptions
{
    public const string SectionName = "Application";

    [Required]
    public string Name { get; init; } = "SharePoint Sync Tool";

    [Required]
    public string Version { get; init; } = "2.0.0";

    public LogLevel LogLevel { get; init; } = LogLevel.Information;

    public bool EnableMetrics { get; init; } = true;

    public bool EnableHealthChecks { get; init; } = true;

    public string Environment { get; init; } = "Production";

    public bool DailyMode { get; init; } = true;

    [Required]
    public string LegacyConfigPath { get; init; } = "XmlConfig\\UserConfig.xml";
}
