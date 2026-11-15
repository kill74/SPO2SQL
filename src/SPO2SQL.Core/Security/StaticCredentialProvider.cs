namespace SPO2SQL.Security;

public sealed class StaticCredentialProvider : ICredentialProvider
{
    private readonly string _username;
    private readonly string _password;
    private readonly string _sqlConnectionString;

    public StaticCredentialProvider(string username, string password, string sqlConnectionString)
    {
        _username = username;
        _password = password;
        _sqlConnectionString = sqlConnectionString;
    }

    public (string Username, string Password, string SqlConnectionString)? GetCredentials()
    {
        return (_username, _password, _sqlConnectionString);
    }
}
