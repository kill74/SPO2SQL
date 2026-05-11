using System;
using System.Collections.Generic;

namespace Bring.Security
{
    public static class CredentialManager
    {
        private static readonly List<ICredentialProvider> Providers;
        private static bool _initialized;

        static CredentialManager()
        {
            Providers = new List<ICredentialProvider>();
        }

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            Providers.Add(new EnvCredentialProvider());
            Providers.Add(new DpapiCredentialProvider());
        }

        public static void RegisterProvider(ICredentialProvider provider)
        {
            EnsureInitialized();
            Providers.Insert(0, provider);
        }

        public static (string Username, string Password) GetSharePointCredentials()
        {
            EnsureInitialized();

            foreach (var provider in Providers)
            {
                var result = provider.GetCredentials();
                if (result != null && !string.IsNullOrEmpty(result.Value.Username) && !string.IsNullOrEmpty(result.Value.Password))
                {
                    Logger.Log(2, $"[CredentialManager] Using provider: {provider.GetType().Name}");
                    return (result.Value.Username, result.Value.Password);
                }
            }

            throw new InvalidOperationException("No credential provider returned valid SharePoint credentials.");
        }

        public static string GetSqlConnectionString()
        {
            EnsureInitialized();

            foreach (var provider in Providers)
            {
                var result = provider.GetCredentials();
                if (result != null && !string.IsNullOrEmpty(result.Value.SqlConnectionString))
                {
                    Logger.Log(2, $"[CredentialManager] Using provider: {provider.GetType().Name}");
                    return result.Value.SqlConnectionString;
                }
            }

            throw new InvalidOperationException("No credential provider returned a valid SQL connection string.");
        }
    }
}
