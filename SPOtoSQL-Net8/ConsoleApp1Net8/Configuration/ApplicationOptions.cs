using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;

namespace Bring.Configuration;

/// <summary>
/// General application configuration options.
/// </summary>
public record ApplicationOptions
{
    public const string SectionName = "Application";

    /// <summary>
    /// Application name for logging and tracking.
    /// </summary>
    public string Name { get; init; } = "SharePoint Sync Tool";

    /// <summary>
    /// Application version.
    /// </summary>
    public string Version { get; init; } = "2.0.0";

    /// <summary>
    /// Minimum log level for console output.
    /// </summary>
    public LogLevel LogLevel { get; init; } = LogLevel.Information;

    /// <summary>
    /// Whether to enable detailed operation metrics.
    /// </summary>
    public bool EnableMetrics { get; init; } = true;

    /// <summary>
    /// Whether to run health checks on startup.
    /// </summary>
    public bool EnableHealthChecks { get; init; } = true;

    /// <summary>
    /// Environment name (Development, Staging, Production).
    /// </summary>
    public string Environment { get; init; } = "Production";
}
