using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SPO2SQL.Security;

public class DpapiCredentialProvider : ICredentialProvider
{
    private sealed record StoredCredentials(string Username, string Password, string SqlConnectionString);

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SPO2SQL",
        "credentials.enc");

    public (string Username, string Password, string SqlConnectionString)? GetCredentials()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            byte[] encrypted = File.ReadAllBytes(FilePath);
            byte[] decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            string plaintext = Encoding.UTF8.GetString(decrypted);

            var credentials = JsonSerializer.Deserialize<StoredCredentials>(plaintext);
            return credentials is { Username.Length: > 0, Password.Length: > 0 }
                ? (credentials.Username, credentials.Password, credentials.SqlConnectionString ?? string.Empty)
                : null;
        }
        catch
        {
            return null;
        }
    }

    public static void SaveCredentials(string username, string password, string connectionString)
    {
        string dir = Path.GetDirectoryName(FilePath);
        Directory.CreateDirectory(dir);

        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(connectionString);

        string plaintext = JsonSerializer.Serialize(
            new StoredCredentials(username, password, connectionString));
        byte[] data = Encoding.UTF8.GetBytes(plaintext);
        byte[] encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);

        File.WriteAllBytes(FilePath, encrypted);
    }
}
