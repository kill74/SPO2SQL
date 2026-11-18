using SPO2SQL.Security;

namespace SPO2SQL;

/// <summary>
/// Implements the --setup-secrets command in the real console host.
/// </summary>
internal static class CredentialSetup
{
    public static int Run()
    {
        Console.WriteLine("=== Secure Credential Setup ===");
        Console.WriteLine("Credentials will be encrypted and stored in the current user's profile.");
        Console.WriteLine();

        string? username = Read("SharePoint Username: ");
        string? password = Read("SharePoint Password: ");
        string? connectionString = Read("SQL Connection String: ");

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            Console.WriteLine("ERROR: Username and password are required.");
            return 1;
        }

        try
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.WriteLine("ERROR: DPAPI encryption is not available on this platform.");
                Console.WriteLine("Use environment variables or user secrets instead.");
                return 1;
            }

            DpapiCredentialProvider.SaveCredentials(username, password, connectionString ?? string.Empty);
            Console.WriteLine("Credentials saved securely.");
            return 0;
        }
        catch (PlatformNotSupportedException)
        {
            Console.WriteLine("ERROR: DPAPI encryption is not available on this platform.");
            Console.WriteLine("Use environment variables or user secrets instead.");
            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Failed to save credentials: {ex.Message}");
            return 1;
        }
    }

    private static string? Read(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine()?.Trim();
    }
}
