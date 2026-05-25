using System;
using System.Net;
using System.Security;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.SharePoint.Client;
using SPO2SQL.Logging;

namespace SPO2SQL.SharePoint;

public class SPOUser : IDisposable
{
    private const string WellKnownClientId = "9bc3ab49-b65d-410a-85ad-de819febfddc";

    public string Username { get; private set; }

    private readonly string _plainPassword;
    private SecureString _securePassword;
    private AuthenticationResult _tokenResult;
    private string _lastSiteUrl;

    internal ICredentials Credentials { get; private set; }

    public SPOUser(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username cannot be null or empty.", nameof(username));
        }

        if (password == null)
        {
            throw new ArgumentNullException(nameof(password), "Password cannot be null.");
        }

        Logger.LogDebug($"Initializing SPOUser for: {username}");

        Username = username;
        _plainPassword = password;

        _securePassword = new SecureString();
        foreach (char c in password)
        {
            _securePassword.AppendChar(c);
        }

        _securePassword.MakeReadOnly();
    }

    public async Task<string> GetAccessTokenAsync(string siteUrl)
    {
        if (!string.IsNullOrEmpty(_tokenResult?.AccessToken) && _lastSiteUrl == siteUrl && _tokenResult.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            return _tokenResult.AccessToken;
        }

        var authority = "https://login.microsoftonline.com/organizations";
        var app = PublicClientApplicationBuilder.Create(WellKnownClientId)
            .WithAuthority(authority)
            .Build();

        var uri = new Uri(siteUrl);
        var scope = $"{uri.Scheme}://{uri.Host}/.default";

        try
        {
            var accounts = await app.GetAccountsAsync();
            _tokenResult = await app.AcquireTokenByUsernamePassword(
                new[] { scope },
                Username,
                _plainPassword)
                .ExecuteAsync();
            _lastSiteUrl = siteUrl;
            Logger.LogDebug("OAuth token acquired successfully for " + siteUrl);
            return _tokenResult.AccessToken;
        }
        catch (MsalUiRequiredException ex)
        {
            Logger.LogError("Interactive login required. This may be due to MFA or Conditional Access policies.", ex);
            throw new InvalidOperationException(
                "SharePoint Online authentication failed: MFA or Conditional Access policy requires interactive login. " +
                "Consider using app-only authentication with a service principal.", ex);
        }
        catch (MsalException ex)
        {
            Logger.LogError($"MSAL authentication failed: {ex.Message}", ex);
            throw;
        }
    }

    public void ApplyAuthentication(ClientContext ctx, string siteUrl)
    {
        ctx.ExecutingWebRequest += async (sender, e) =>
        {
            try
            {
                string token = await GetAccessTokenAsync(siteUrl);
                e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + token;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to apply authentication token", ex);
            }
        };
    }

    public void Dispose()
    {
        _securePassword?.Dispose();
        GC.SuppressFinalize(this);
    }
}
