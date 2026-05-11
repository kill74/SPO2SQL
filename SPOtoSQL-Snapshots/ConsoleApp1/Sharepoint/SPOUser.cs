using Microsoft.SharePoint.Client;
using System;
using System.Net;
using System.Security;

namespace Bring.Sharepoint
{
    public class SPOUser : IDisposable
    {
        public string Username { get; private set; }

        private SecureString _securePassword;

        internal ICredentials Credentials { get; private set; }

        public SPOUser(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be null or empty.", nameof(username));
            if (password == null)
                throw new ArgumentNullException(nameof(password), "Password cannot be null.");

            Logger.LogDebug($"Initializing SPOUser for: {username}");

            Username = username;

            _securePassword = new SecureString();
            foreach (char c in password)
                _securePassword.AppendChar(c);
            _securePassword.MakeReadOnly();

#if NETFRAMEWORK
            Credentials = new SharePointOnlineCredentials(Username, _securePassword);
#else
            Credentials = new NetworkCredential(Username, password);
#endif
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