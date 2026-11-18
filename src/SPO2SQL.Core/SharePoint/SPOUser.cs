using System.Net;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Identity.Client;
using Microsoft.SharePoint.Client;
using SPO2SQL.Logging;

namespace SPO2SQL.SharePoint;

public sealed class SPOUser : IDisposable
{
    private const string WellKnownClientId = "9bc3ab49-b65d-410a-85ad-de819febfddc";

    private readonly string _plainPassword;
    private readonly SecureString? _securePassword;
    private readonly IPublicClientApplication? _publicApp;
    private readonly IConfidentialClientApplication? _confidentialApp;
    private readonly X509Certificate2? _certificate;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    private AuthenticationResult? _tokenResult;
    private string? _lastSiteUrl;
    private bool _disposed;

    public string Username { get; }

    internal ICredentials? Credentials { get; }

    /// <summary>
    /// Legacy username/password (ROPC) authentication. Kept for environments that
    /// cannot yet use an app-only service principal.
    /// </summary>
    public SPOUser(string username, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentNullException.ThrowIfNull(password);

        Username = username;
        _plainPassword = password;
        Credentials = new NetworkCredential(username, password);

        var securePassword = new SecureString();
        foreach (char c in password)
        {
            securePassword.AppendChar(c);
        }

        securePassword.MakeReadOnly();
        _securePassword = securePassword;

        _publicApp = PublicClientApplicationBuilder.Create(WellKnownClientId)
            .WithAuthority("https://login.microsoftonline.com/organizations")
            .Build();
    }

    /// <summary>
    /// App-only authentication with a certificate. This is the recommended mode for
    /// unattended jobs because it is not blocked by MFA or Conditional Access.
    /// </summary>
    public SPOUser(AppOnlyAuthSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.TenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.ClientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.CertificatePath);

        _plainPassword = string.Empty;
        Username = $"app:{settings.ClientId}";

        _certificate = string.IsNullOrWhiteSpace(settings.CertificatePassword)
            ? new X509Certificate2(settings.CertificatePath)
            : new X509Certificate2(settings.CertificatePath, settings.CertificatePassword);

        _confidentialApp = ConfidentialClientApplicationBuilder.Create(settings.ClientId)
            .WithAuthority($"https://login.microsoftonline.com/{settings.TenantId}")
            .WithCertificate(_certificate)
            .Build();

        Logger.LogInformation($"Using app-only authentication for client {settings.ClientId}");
    }

    public async Task<string> GetAccessTokenAsync(string siteUrl)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteUrl);

        await _tokenLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_tokenResult != null &&
                _lastSiteUrl == siteUrl &&
                _tokenResult.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(5))
            {
                return _tokenResult.AccessToken;
            }

            var uri = new Uri(siteUrl);
            string scope = $"{uri.Scheme}://{uri.Host}/.default";

            AuthenticationResult result;
            if (_confidentialApp != null)
            {
                result = await _confidentialApp
                    .AcquireTokenForClient([scope])
                    .ExecuteAsync()
                    .ConfigureAwait(false);
            }
            else
            {
                try
                {
                    result = await _publicApp!
                        .AcquireTokenByUsernamePassword([scope], Username, _plainPassword)
                        .ExecuteAsync()
                        .ConfigureAwait(false);
                }
                catch (MsalUiRequiredException ex)
                {
                    Logger.LogError("Interactive login required. MFA or Conditional Access is blocking ROPC.", ex);
                    throw new InvalidOperationException(
                        "SharePoint Online authentication failed: MFA or Conditional Access policy requires interactive login. " +
                        "Configure app-only authentication (TenantId, ClientId and CertificatePath).", ex);
                }
            }

            _tokenResult = result;
            _lastSiteUrl = siteUrl;
            Logger.LogDebug("OAuth token acquired successfully for " + siteUrl);
            return result.AccessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    public void ApplyAuthentication(ClientContext ctx, string siteUrl)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteUrl);

        ctx.ExecutingWebRequest += (_, e) =>
        {
            string token = GetAccessTokenAsync(siteUrl).GetAwaiter().GetResult();
            e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + token;
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _securePassword?.Dispose();
        _certificate?.Dispose();
        _tokenLock.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
