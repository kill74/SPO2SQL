using System;

namespace SPO2SQL.Security;

public class EnvCredentialProvider : ICredentialProvider
{
    public (string Username, string Password, string SqlConnectionString)? GetCredentials()
    {
        string user = Environment.GetEnvironmentVariable("SPO_USERNAME");
        string pass = Environment.GetEnvironmentVariable("SPO_PASSWORD");
        string conn = Environment.GetEnvironmentVariable("SQL_CONNECTION_STRING");

        if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(pass))
        {
            return (user, pass, conn ?? string.Empty);
        }

        return null;
    }
}
