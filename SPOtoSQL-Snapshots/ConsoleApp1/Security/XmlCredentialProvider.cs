using Bring.XmlConfig;

namespace Bring.Security
{
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
}
