using Microsoft.SharePoint.Client;
using System;
using System.Security;

namespace Bring.Sharepoint
{
    /// <summary>
    /// Represents a user context for authenticating against SharePoint Online.
    /// Wraps username and password into a SecureString and SharePointOnlineCredentials.
    /// </summary>
    public class SPOUser
    {
        /// <summary>
        /// The user's login name (usually an email address).
        /// </summary>
        public string Username { get; private set; }

        // Backing SecureString for the user's password
        private SecureString securePassword;

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
        public SPOUser(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be null or empty.", nameof(username));
            if (password == null)
                throw new ArgumentNullException(nameof(password), "Password cannot be null.");

            Console.WriteLine($"Initializing SPOUser for: {username}");

            this.Username = username;

            // Convert each character of the plaintext password into a SecureString
            this.securePassword = new SecureString();
            foreach (char c in password)
                this.securePassword.AppendChar(c);
            this.securePassword.MakeReadOnly();

            // Create SharePointOnlineCredentials for authentication
            this.Credentials = new SharePointOnlineCredentials(this.Username, this.securePassword);
        }
    }
}
