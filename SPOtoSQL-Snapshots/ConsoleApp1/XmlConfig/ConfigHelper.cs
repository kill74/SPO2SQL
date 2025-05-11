using System;
using System.Xml;

namespace Bring.XmlConfig
{
    /// <summary>
    /// Provides methods to read SharePoint credentials and SQL connection
    /// strings from an XML configuration file.
    /// </summary>
    public class ConfigurationReader
    {
        // Path to the XML configuration file relative to application root
        private static readonly string CONFIG_PATH = "XmlConfig/UserConfig.xml";

        // Singleton XmlDocument instance, loaded on first access
        private static XmlDocument _xmlDoc;

        /// <summary>
        /// Loads the XML configuration document if it hasn't been loaded yet.
        /// Throws an exception if the file cannot be read.
        /// </summary>
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
                    // Wrap any load errors in a descriptive exception
                    throw new Exception($"Error loading configuration file: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Retrieves SharePoint credentials (username and password)
        /// from the configuration file.
        /// </summary>
        /// <returns>
        /// A tuple containing the Username and Password nodes' inner text.
        /// </returns>
        /// <exception cref="Exception">
        /// Thrown if the configuration cannot be loaded or the expected nodes are missing.
        /// </exception>
        public static (string Username, string Password) GetSharePointCredentials()
        {
            // Ensure configuration is loaded into memory
            LoadConfig();

            // Select the SharePoint node under the root Configuration element
            var spNode = _xmlDoc.SelectSingleNode("//Configuration/SharePoint");
            if (spNode == null)
                throw new Exception("SharePoint configuration section not found.");

            // Extract Username and Password elements
            var usernameNode = spNode.SelectSingleNode("Username");
            var passwordNode = spNode.SelectSingleNode("Password");
            if (usernameNode == null || passwordNode == null)
                throw new Exception("Missing Username or Password element in SharePoint configuration.");

            return (
                usernameNode.InnerText.Trim(),
                passwordNode.InnerText.Trim()
            );
        }

        /// <summary>
        /// Retrieves the SQL Server connection string from the configuration file.
        /// </summary>
        /// <returns>
        /// The inner text of the ConnectionString element under SQL configuration.
        /// </returns>
        /// <exception cref="Exception">
        /// Thrown if the configuration cannot be loaded or the connection string node is missing.
        /// </exception>
        public static string GetSqlConnectionString()
        {
            // Ensure configuration is loaded into memory
            LoadConfig();

            // Select the ConnectionString element under SQL section
            var connNode = _xmlDoc.SelectSingleNode("//Configuration/SQL/ConnectionString");
            if (connNode == null)
                throw new Exception("SQL ConnectionString element not found in configuration.");

            return connNode.InnerText.Trim();
        }
    }
}
