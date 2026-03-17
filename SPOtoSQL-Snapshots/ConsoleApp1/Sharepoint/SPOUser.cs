using Microsoft.SharePoint.Client;
using System;
using System.Security;

namespace Bring.Sharepoint
{
    /// <summary>
    /// Represents a user context for authenticating against SharePoint Online.
    /// Wraps username and password into a SecureString and SharePointOnlineCredentials.
    /// Implements IDisposable to properly clean up SecureString password data.
    /// </summary>
    public class SPOUser : IDisposable
    {
        /// <summary>
        /// The user's login name (usually an email address).
        /// </summary>
        public string Username { get; private set; }

        /// <summary>
        /// Backing SecureString for the user's password.
        /// </summary>
        private SecureString _securePassword;

        /// <summary>
        /// Credentials object used by the SharePoint client to authenticate requests.
        /// </summary>
        internal SharePointOnlineCredentials Credentials { get; private set; }

        /// <summary>
        /// Initializes a new SPOUser with the given credentials.
        /// Converts the plaintext password into a SecureString.
        /// </summary>
        /// <param name="username">The SharePoint username (e.g., user@tenant.onmicrosoft.com).</param>
        /// <param name="password">The user's plaintext password (will be secured internally).</param>
        /// <exception cref="ArgumentException">Thrown if username is null or empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown if password is null.</exception>
        public SPOUser(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be null or empty.", nameof(username));
            if (password == null)
                throw new ArgumentNullException(nameof(password), "Password cannot be null.");

            Logger.LogDebug($"Initializing SPOUser for: {username}");

            Username = username;

            // Convert plaintext password to SecureString
            _securePassword = new SecureString();
            foreach (char c in password)
                _securePassword.AppendChar(c);
            _securePassword.MakeReadOnly();

            // Create SharePointOnlineCredentials for authentication
            Credentials = new SharePointOnlineCredentials(Username, _securePassword);
        }

        /// <summary>
        /// Disposes of the SecureString password, clearing it from memory.
        /// </summary>
        public void Dispose()
        {
            _securePassword?.Dispose();
        }
    }
}