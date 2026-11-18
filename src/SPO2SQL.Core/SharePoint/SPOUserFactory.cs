using SPO2SQL.Configuration;

namespace SPO2SQL.SharePoint;

/// <summary>
/// Parameters for app-only (client credentials) authentication.
/// </summary>
public sealed record AppOnlyAuthSettings(
    string TenantId,
    string ClientId,
    string CertificatePath,
    string? CertificatePassword);

/// <summary>
/// Creates the authentication context used by every SharePoint operation.
/// </summary>
public static class SPOUserFactory
{
    public static SPOUser Create(SyncSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return settings.HasAppOnlyAuth
            ? new SPOUser(new AppOnlyAuthSettings(
                settings.TenantId!,
                settings.ClientId!,
                settings.CertificatePath!,
                settings.CertificatePassword))
            : new SPOUser(settings.Username, settings.Password);
    }
}
