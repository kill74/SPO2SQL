namespace SPO2SQL.Security;

public class EnvCredentialProvider : ICredentialProvider
{
    public (string Username, string Password, string SqlConnectionString)? GetCredentials()
    {
        string user = Environment.GetEnvironmentVariable("SPO_USERNAME") ?? string.Empty;
        string pass = Environment.GetEnvironmentVariable("SPO_PASSWORD") ?? string.Empty;
        string conn = Environment.GetEnvironmentVariable("SQL_CONNECTION_STRING") ?? string.Empty;

        return !string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(pass)
            ? (user, pass, conn)
            : null;
    }
}
