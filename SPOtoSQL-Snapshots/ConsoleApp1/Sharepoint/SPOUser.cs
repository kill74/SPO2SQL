using Microsoft.SharePoint.Client;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

namespace Bring.Sharepoint
{
    public class SPOUser : IDisposable
    {
        private const string WellKnownClientId = "9bc3ab49-b65d-410a-85ad-de819febfddc";

        public string Username { get; private set; }

        private readonly string _plainPassword;
        private AuthenticationResult _tokenResult;
        private string _lastSiteUrl;
        private IPublicClientApplication _app;
        private readonly ConcurrentDictionary<string, IDisposable> _eventHandlers = new();

        public SPOUser(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be null or empty.", nameof(username));
            if (password == null)
                throw new ArgumentNullException(nameof(password), "Password cannot be null.");

            Logger.LogDebug($"Initializing SPOUser for: {username}");

            Username = username;
            _plainPassword = password;
        }

        /// <summary>
        /// Acquires or returns a cached OAuth access token for the given SharePoint site URL.
        /// Uses MSAL with token cache (silent first, then username/password fallback).
        /// </summary>
        public async Task<string> GetAccessTokenAsync(string siteUrl)
        {
            if (!string.IsNullOrEmpty(_tokenResult?.AccessToken) &&
                _lastSiteUrl == siteUrl &&
                _tokenResult.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(5))
            {
                return _tokenResult.AccessToken;
            }

            _app ??= PublicClientApplicationBuilder.Create(WellKnownClientId)
                .WithAuthority("https://login.microsoftonline.com/organizations")
                .Build();

            var uri = new Uri(siteUrl);
            var scope = $"{uri.Scheme}://{uri.Host}/.default";

            try
            {
                var accounts = await _app.GetAccountsAsync();

                if (accounts.Any())
                {
                    try
                    {
                        _tokenResult = await _app.AcquireTokenSilent(new[] { scope }, accounts.First())
                            .ExecuteAsync();
                        _lastSiteUrl = siteUrl;
                        Logger.LogDebug("OAuth token acquired silently for " + siteUrl);
                        return _tokenResult.AccessToken;
                    }
                    catch (MsalUiRequiredException)
                    {
                    }
                }

                _tokenResult = await _app.AcquireTokenByUsernamePassword(
                    new[] { scope },
                    Username,
                    _plainPassword)
                    .ExecuteAsync();
                _lastSiteUrl = siteUrl;
                Logger.LogDebug("OAuth token acquired via username/password for " + siteUrl);
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

        /// <summary>
        /// Applies the OAuth bearer token to a ClientContext via the ExecutingWebRequest event.
        /// Unsubscribes previous handler for the same site URL to prevent leaks.
        /// </summary>
        public void ApplyAuthentication(ClientContext ctx, string siteUrl)
        {
            if (_eventHandlers.TryGetValue(siteUrl, out var oldHandler))
            {
                oldHandler.Dispose();
                _eventHandlers.TryRemove(siteUrl, out _);
            }

            var handler = new EventHandler<WebRequestEventArgs>(async (sender, e) =>
            {
                try
                {
                    string token = await GetAccessTokenAsync(siteUrl);
                    e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + token;
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to apply authentication token", ex);
                    throw;
                }
            });

            ctx.ExecutingWebRequest += handler;
            _eventHandlers[siteUrl] = new HandlerDisposable(ctx, handler);
        }

        /// <summary>
        /// Clears references to sensitive data when the object is disposed.
        /// </summary>
        public void Dispose()
        {
            foreach (var kvp in _eventHandlers)
                kvp.Value.Dispose();
            _eventHandlers.Clear();
        }

        private sealed class HandlerDisposable : IDisposable
        {
            private readonly ClientContext _ctx;
            private readonly EventHandler<WebRequestEventArgs> _handler;

            public HandlerDisposable(ClientContext ctx, EventHandler<WebRequestEventArgs> handler)
            {
                _ctx = ctx;
                _handler = handler;
            }

            public void Dispose()
            {
                _ctx.ExecutingWebRequest -= _handler;
            }
        }
    }
}