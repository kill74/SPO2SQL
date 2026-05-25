namespace SPO2SQL.Security;

public interface ICredentialProvider
{
    (string Username, string Password, string SqlConnectionString)? GetCredentials();
}
