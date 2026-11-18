using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SPO2SQL.Security;

/// <summary>
/// Windows DPAPI credential store. All entry points fail fast on other platforms so
/// callers can fall back to environment variables or user secrets.
/// </summary>
public class DpapiCredentialProvider : ICredentialProvider
{
    private sealed record StoredCredentials(string Username, string Password, string SqlConnectionString);

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SPO2SQL",
        "credentials.enc");

    public (string Username, string Password, string SqlConnectionString)? GetCredentials()
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(FilePath))
        {
            return null;
        }

        try
        {
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

    [SupportedOSPlatform("windows")]
    public static void SaveCredentials(string username, string password, string connectionString)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("DPAPI credential storage is only available on Windows.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(connectionString);

        string dir = Path.GetDirectoryName(FilePath)
            ?? throw new InvalidOperationException("Could not determine the credential storage directory.");
        Directory.CreateDirectory(dir);

        string plaintext = JsonSerializer.Serialize(
            new StoredCredentials(username, password, connectionString));
        byte[] data = Encoding.UTF8.GetBytes(plaintext);
        byte[] encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);

        File.WriteAllBytes(FilePath, encrypted);
    }
}
