using System;
using System.Xml;

namespace Bring.XmlConfig
{
    public class ConfigurationReader
    {
        private static readonly string CONFIG_PATH = "XmlConfig/UserConfig.xml";
        private static XmlDocument _xmlDoc;

        private static void LoadConfig()
        {
            if (_xmlDoc == null)
            {
                try
                {
                    _xmlDoc = new XmlDocument();
                    _xmlDoc.Load(CONFIG_PATH);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error loading configuration file: {ex.Message}");
                }
            }
        }

        public static (string Username, string Password) GetSharePointCredentials()
        {
            LoadConfig();
            var spNode = _xmlDoc.SelectSingleNode("//Configuration/SharePoint");
            return (
                spNode.SelectSingleNode("Username").InnerText,
                spNode.SelectSingleNode("Password").InnerText
            );
        }

        public static string GetSqlConnectionString()
        {
            LoadConfig();
            return _xmlDoc.SelectSingleNode("//Configuration/SQL/ConnectionString").InnerText;
        }
    }
}