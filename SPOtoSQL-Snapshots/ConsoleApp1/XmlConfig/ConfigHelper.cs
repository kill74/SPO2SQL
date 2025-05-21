using System;
using System.Collections.Generic;
using System.Xml;
using System.Linq;

namespace Bring.XmlConfig
{
    /// <summary>
    /// Provides methods to read SharePoint credentials, SQL connection strings, 
    /// and configuration settings from an XML configuration file.
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
        /// <exception cref="Exception">Thrown when the configuration file cannot be loaded.</exception>
        private static void LoadConfig()
        {
            if (_xmlDoc == null)
            {
                try
                {
                    _xmlDoc = new XmlDocument();
                    _xmlDoc.Load(CONFIG_PATH);
                    Console.WriteLine("Configuration file loaded successfully.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load configuration file: {ex.Message}");
                    throw new Exception($"Error loading configuration file: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Gets the list of columns that should be included in the replication process.
        /// </summary>
        /// <returns>
        /// A HashSet of column names to include, or null if all columns should be included.
        /// </returns>
        public static HashSet<string> GetSelectedColumns()
        {
            LoadConfig();

            try
            {
                var columnNodes = _xmlDoc.SelectNodes("//Configuration/ReplicationConfiguration/SelectColumns/Column");

                // If no column nodes exist, return null to indicate all columns should be included
                if (columnNodes == null || columnNodes.Count == 0)
                {
                    Console.WriteLine("No specific columns configured. All columns will be included.");
                    return null;
                }

                // Create case-insensitive HashSet for column names
                var selectedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (XmlNode node in columnNodes)
                {
                    if (!string.IsNullOrWhiteSpace(node.InnerText))
                    {
                        string columnName = node.InnerText.Trim();
                        selectedColumns.Add(columnName);
                        Console.WriteLine($"Added selected column: {columnName}");
                    }
                }

                // If no valid columns were added, return null to include all columns
                if (selectedColumns.Count == 0)
                {
                    Console.WriteLine("No valid columns specified. All columns will be included.");
                    return null;
                }

                Console.WriteLine($"Total selected columns: {selectedColumns.Count}");
                return selectedColumns;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading selected columns: {ex.Message}");
                throw new Exception($"Error reading selected columns configuration: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves SharePoint credentials (username and password) from the configuration file.
        /// </summary>
        /// <returns>A tuple containing the Username and Password.</returns>
        /// <exception cref="Exception">Thrown when required configuration elements are missing.</exception>
        public static (string Username, string Password) GetSharePointCredentials()
        {
            LoadConfig();

            try
            {
                var spNode = _xmlDoc.SelectSingleNode("//Configuration/SharePoint");
                if (spNode == null)
                    throw new Exception("SharePoint configuration section not found.");

                var usernameNode = spNode.SelectSingleNode("Username");
                var passwordNode = spNode.SelectSingleNode("Password");

                if (usernameNode == null || passwordNode == null)
                    throw new Exception("Missing Username or Password element in SharePoint configuration.");

                var username = usernameNode.InnerText.Trim();
                var password = passwordNode.InnerText.Trim();

                // Basic validation
                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                    throw new Exception("Username or Password cannot be empty.");

                Console.WriteLine("SharePoint credentials retrieved successfully.");
                return (username, password);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving SharePoint credentials: {ex.Message}");
                throw new Exception("Failed to retrieve SharePoint credentials.", ex);
            }
        }

        /// <summary>
        /// Retrieves the SQL Server connection string from the configuration file.
        /// </summary>
        /// <returns>The SQL Server connection string.</returns>
        /// <exception cref="Exception">Thrown when the connection string configuration is missing or invalid.</exception>
        public static string GetSqlConnectionString()
        {
            LoadConfig();

            try
            {
                var connNode = _xmlDoc.SelectSingleNode("//Configuration/SQL/ConnectionString");
                if (connNode == null)
                    throw new Exception("SQL ConnectionString element not found in configuration.");

                string connectionString = connNode.InnerText.Trim();
                if (string.IsNullOrEmpty(connectionString))
                    throw new Exception("SQL connection string cannot be empty.");

                Console.WriteLine("SQL connection string retrieved successfully.");
                return connectionString;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving SQL connection string: {ex.Message}");
                throw new Exception("Failed to retrieve SQL connection string.", ex);
            }
        }

        /// <summary>
        /// Retrieves SharePoint list configuration information.
        /// </summary>
        /// <returns>A collection of SharePoint list configurations.</returns>
        public static IEnumerable<SharePointListConfig> GetSharePointListConfigurations()
        {
            LoadConfig();

            try
            {
                var listNodes = _xmlDoc.SelectNodes("//Configuration/ReplicationConfiguration/SharePointLists/List");
                if (listNodes == null || listNodes.Count == 0)
                {
                    Console.WriteLine("No SharePoint list configurations found.");
                    return Enumerable.Empty<SharePointListConfig>();
                }

                var configurations = new List<SharePointListConfig>();

                foreach (XmlNode listNode in listNodes)
                {
                    var config = new SharePointListConfig
                    {
                        SiteUrl = GetNodeValue(listNode, "SiteUrl"),
                        ListTitle = GetNodeValue(listNode, "ListTitle"),
                        SqlTable = GetNodeValue(listNode, "SqlTable"),
                        AutoAddNewColumns = GetNodeValueBool(listNode, "AutoAddNewColumns", true),
                        Disabled = GetNodeValueBool(listNode, "Disabled", false)
                    };

                    if (IsValidListConfig(config))
                    {
                        configurations.Add(config);
                        Console.WriteLine($"Loaded configuration for list: {config.ListTitle}");
                    }
                }

                return configurations;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading SharePoint list configurations: {ex.Message}");
                throw new Exception("Failed to read SharePoint list configurations.", ex);
            }
        }

        /// <summary>
        /// Gets a node's inner text value.
        /// </summary>
        private static string GetNodeValue(XmlNode parentNode, string nodeName)
        {
            var node = parentNode.SelectSingleNode(nodeName);
            return node?.InnerText.Trim() ?? string.Empty;
        }

        /// <summary>
        /// Gets a node's boolean value.
        /// </summary>
        private static bool GetNodeValueBool(XmlNode parentNode, string nodeName, bool defaultValue)
        {
            var value = GetNodeValue(parentNode, nodeName);
            return !string.IsNullOrEmpty(value) && bool.TryParse(value, out bool result)
                ? result
                : defaultValue;
        }

        /// <summary>
        /// Validates a SharePoint list configuration.
        /// </summary>
        private static bool IsValidListConfig(SharePointListConfig config)
        {
            if (string.IsNullOrEmpty(config.SiteUrl))
            {
                Console.WriteLine("Invalid configuration: SiteUrl is required.");
                return false;
            }

            if (string.IsNullOrEmpty(config.ListTitle))
            {
                Console.WriteLine("Invalid configuration: ListTitle is required.");
                return false;
            }

            if (string.IsNullOrEmpty(config.SqlTable))
            {
                Console.WriteLine("Invalid configuration: SqlTable is required.");
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Represents the configuration for a SharePoint list.
    /// </summary>
    public class SharePointListConfig
    {
        public string SiteUrl { get; set; }
        public string ListTitle { get; set; }
        public string SqlTable { get; set; }
        public bool AutoAddNewColumns { get; set; }
        public bool Disabled { get; set; }
    }
}