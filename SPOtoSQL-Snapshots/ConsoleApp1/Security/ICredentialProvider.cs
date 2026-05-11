namespace Bring.Security
{
    public interface ICredentialProvider
    {
        (string Username, string Password, string SqlConnectionString)? GetCredentials();
    }
}
