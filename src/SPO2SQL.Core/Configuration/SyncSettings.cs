namespace SPO2SQL.Configuration;

/// <summary>
/// Immutable snapshot of the settings needed by the synchronization layer.
/// The console host maps its validated options into this record once, at startup,
/// so the core never has to reach back into global configuration state.
/// </summary>
public sealed record SyncSettings
{
    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public required string SiteUrl { get; init; }

    public required string SqlConnectionString { get; init; }

    public string? TenantId { get; init; }

    public string? ClientId { get; init; }

    public string? CertificatePath { get; init; }

    public string? CertificatePassword { get; init; }

    public int CommandTimeoutSeconds { get; init; } = 300;

    public int BatchSize { get; init; } = 80;

    public int MaxRetries { get; init; } = 3;

    public int InitialRetryDelayMs { get; init; } = 1000;

    public bool EnableMetrics { get; init; }

    public string? LegacyConfigPath { get; init; }

    public bool HasAppOnlyAuth =>
        !string.IsNullOrWhiteSpace(TenantId) &&
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(CertificatePath);
}
