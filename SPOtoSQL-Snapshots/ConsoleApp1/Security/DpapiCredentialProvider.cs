using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Bring.Security
{
    public class DpapiCredentialProvider : ICredentialProvider
    {
        private string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SPO2SQL",
            "credentials.enc");

        public (string Username, string Password, string SqlConnectionString)? GetCredentials()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return null;

                byte[] encrypted = File.ReadAllBytes(FilePath);
                byte[] decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                string plaintext = Encoding.UTF8.GetString(decrypted);

                string[] parts = plaintext.Split('|');
                return parts.Length >= 3
                    ? (parts[0], parts[1], parts[2])
                    : null;
            }
            catch
            {
                return null;
            }
        }

        public void SaveCredentials(string username, string password, string connectionString)
        {
            string dir = Path.GetDirectoryName(FilePath);
            Directory.CreateDirectory(dir);

            string plaintext = $"{username}|{password}|{connectionString}";
            byte[] data = Encoding.UTF8.GetBytes(plaintext);
            byte[] encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);

            File.WriteAllBytes(FilePath, encrypted);
        }
    }
}
