using SPO2SQL.XmlConfig;

namespace SPO2SQL.Security;

public class XmlCredentialProvider : ICredentialProvider
{
    public (string Username, string Password, string SqlConnectionString)? GetCredentials()
    {
        try
        {
            var (user, pass) = ConfigurationReader.GetSharePointCredentials();
            string conn = ConfigurationReader.GetSqlConnectionString();
            return (user, pass, conn);
        }
        catch
        {
            return null;
        }
    }
}
