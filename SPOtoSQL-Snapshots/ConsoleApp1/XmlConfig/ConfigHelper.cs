using System;
using System.Collections.Generic;
using System.Xml;
using Bring.SPODataQuality;
using System.Linq;

namespace Bring.XmlConfig
{
    /// <summary>
    /// Provides methods to read SharePoint credentials, SQL connection strings, 
    /// and configuration settings from an XML configuration file.
    /// </summary>
    public static class ConfigurationReader
    {
        private static string _configPath = "SPO_to_SQL_config.xml"; // valor padrão
        private static XmlDocument _xmlDoc;

        public static void SetConfigPath(string path)
        {
            _configPath = path;
            _xmlDoc = null; // força recarregar se já estava carregado
        }

        /// <summary>
        /// Loads the XML configuration document if it hasn't been loaded yet.
        /// Throws an exception if the file cannot be read.
        /// </summary>
        /// <exception cref="Exception">Thrown when the configuration file cannot be loaded.</exception>
        private static void LoadConfig()
        {
            // Lazy loading pattern - only load once
            if (_xmlDoc == null)
            {
                try
                {
                    _xmlDoc = new XmlDocument();
                    _xmlDoc.Load(_configPath);
                    Logger.Log(2, "Configuration file loaded successfully from " + _configPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to load configuration file: " + ex.Message);
                    throw new Exception("Error loading configuration file: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Gets the list of columns that should be included in the replication process.
        /// </summary>
        /// <returns>
        /// A Dictionary of column mappings, or null if all columns should be included.
        /// </returns>
        public static Dictionary<string, ColumnMapping> GetSelectedColumns(string listName = null)
        {
            LoadConfig();

            try
            {
                // List-specific configurations take precedence over global settings
                if (!string.IsNullOrEmpty(listName))
                {
                    var listConfigs = GetListConfigurations();
                    if (listConfigs != null && listConfigs.TryGetValue(listName, out var listConfig))
                    {
                        // Skip ignored lists entirely
                        if (listConfig.Ignore)
                        {
                            Logger.Log(2, "List " + listName + " is configured to be ignored.");
                            return null;
                        }
                        // Use list-specific column configuration if available
                        if (listConfig.Columns != null)
                        {
                            Logger.Log(1, "Using specific configuration for list: " + listName);
                            return listConfig.Columns;
                        }
                    }
                }

                // Fall back to global column configuration
                var columnNodes = _xmlDoc.SelectNodes("//Configuration/ReplicationConfiguration/SelectColumns/column");

                // No specific columns configured = include all columns
                if (columnNodes == null || columnNodes.Count == 0)
                {
                    Logger.Log(2, "No specific columns configured. All columns will be included.");
                    return null;
                }

                // Build case-insensitive column mappings dictionary
                var columnMappings = new Dictionary<string, ColumnMapping>(StringComparer.OrdinalIgnoreCase);

                foreach (XmlNode node in columnNodes)
                {
                    var sourceAttr = node.Attributes["source"];
                    var destAttr = node.Attributes["destination"];
                    var ignoreAttr = node.Attributes["ignore"];

                    if (sourceAttr != null)
                    {
                        var mapping = new ColumnMapping
                        {
                            Source = sourceAttr.Value,
                            Destination = destAttr?.Value ?? sourceAttr.Value, // Default to source name if no destination
                            Ignore = ignoreAttr != null && bool.Parse(ignoreAttr.Value)
                        };

                        columnMappings[mapping.Source] = mapping;
                        Logger.Log(1, "Added column mapping: " + mapping.Source + " -> " + mapping.Destination + " (Ignore: " + mapping.Ignore + ")");
                    }
                }

                return columnMappings.Count > 0 ? columnMappings : null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error reading selected columns: " + ex.Message);
                throw new Exception("Error reading selected columns configuration: " + ex.Message);
            }
        }

        /// <summary>
        /// Gets the list of columns that should be ignored during replication.
        /// </summary>
        /// <returns>A HashSet of column names to ignore, or null if no columns should be ignored.</returns>
        public static HashSet<string> GetIgnoredColumns()
        {
            LoadConfig();

            try
            {
                var columnNodes = _xmlDoc.SelectNodes("//Configuration/ReplicationConfiguration/GlobalIgnore/Column");

                // If no ignore nodes exist, return null to indicate no columns should be ignored
                if (columnNodes == null || columnNodes.Count == 0)
                {
                    Logger.Log(2, "No columns configured to ignore.");
                    return null;
                }

                // Create case-insensitive HashSet for ignored column names
                var ignoredColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (XmlNode node in columnNodes)
                {
                    if (!string.IsNullOrWhiteSpace(node.InnerText))
                    {
                        string columnName = node.InnerText.Trim();
                        ignoredColumns.Add(columnName);
                        Logger.Log(1, "Added ignored column: " + columnName);
                    }
                }

                // Return null if no valid columns were added to ignore
                if (ignoredColumns.Count == 0)
                {
                    Logger.Log(2, "No valid columns specified to ignore.");
                    return null;
                }

                Logger.Log(2, "Total ignored columns: " + ignoredColumns.Count);
                return ignoredColumns;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error reading ignored columns: " + ex.Message);
                throw new Exception("Error reading ignored columns configuration: " + ex.Message);
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

                Logger.Log(2, "SharePoint credentials retrieved successfully.");
                return (username, password);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error retrieving SharePoint credentials: " + ex.Message);
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

                Logger.Log(2, "SQL connection string retrieved successfully.");
                return connectionString;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error retrieving SQL connection string: " + ex.Message);
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
                    Logger.Log(2, "No SharePoint list configurations found.");
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

                    // Only add valid configurations
                    if (IsValidListConfig(config))
                    {
                        configurations.Add(config);
                        Logger.Log(1, "Loaded configuration for list: " + config.ListTitle);
                    }
                }

                return configurations;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error reading SharePoint list configurations: " + ex.Message);
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
            // All three fields are required for proper replication
            if (string.IsNullOrEmpty(config.SiteUrl))
            {
                Logger.Log(1, "Invalid configuration: SiteUrl is required.");
                return false;
            }

            if (string.IsNullOrEmpty(config.ListTitle))
            {
                Logger.Log(1, "Invalid configuration: ListTitle is required.");
                return false;
            }

            if (string.IsNullOrEmpty(config.SqlTable))
            {
                Logger.Log(1, "Invalid configuration: SqlTable is required.");
                return false;
            }

            return true;
        }

        public static Dictionary<string, ListConfiguration> GetListConfigurations()
        {
            LoadConfig();

            try
            {
                var listNodes = _xmlDoc.SelectNodes("//Configuration/ReplicationConfiguration/Lists/List");
                if (listNodes == null || listNodes.Count == 0)
                    return null;

                // Case-insensitive dictionary for list name lookups
                var listConfigs = new Dictionary<string, ListConfiguration>(StringComparer.OrdinalIgnoreCase);

                foreach (XmlNode listNode in listNodes)
                {
                    var nameAttr = listNode.Attributes["name"];
                    var contextAttr = listNode.Attributes["sharepointlist"];
                    var ignoreAttr = listNode.Attributes["ignore"];

                    if (nameAttr != null)
                    {
                        var listConfig = new ListConfiguration
                        {
                            Name = nameAttr.Value,
                            SharepointList = contextAttr?.Value,
                            Ignore = ignoreAttr != null && bool.Parse(ignoreAttr.Value),
                            Columns = GetListColumns(listNode) // Get list-specific column mappings
                        };

                        listConfigs[listConfig.Name] = listConfig;
                    }
                }

                return listConfigs.Count > 0 ? listConfigs : null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error reading list configurations: " + ex.Message);
                throw;
            }
        }

        private static Dictionary<string, ColumnMapping> GetListColumns(XmlNode listNode)
        {
            var columnNodes = listNode.SelectNodes(".//Columns/column");
            if (columnNodes == null || columnNodes.Count == 0)
                return null;

            var columnMappings = new Dictionary<string, ColumnMapping>(StringComparer.OrdinalIgnoreCase);

            foreach (XmlNode node in columnNodes)
            {
                var sourceAttr = node.Attributes["source"];
                var destAttr = node.Attributes["destination"];
                var ignoreAttr = node.Attributes["ignore"];
                var datatypeAttr = node.Attributes["datatype"];

                if (sourceAttr != null)
                {
                    var mapping = new ColumnMapping
                    {
                        Source = sourceAttr.Value,
                        Destination = destAttr?.Value ?? sourceAttr.Value,
                        Ignore = ignoreAttr != null && bool.Parse(ignoreAttr.Value),
                        DataType = datatypeAttr?.Value
                    };

                    columnMappings[mapping.Source] = mapping;
                }
            }

            return columnMappings.Count > 0 ? columnMappings : null;
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

    // Represents mapping between source and destination columns with ignore capability
    public class ColumnMapping
    {
        public string Source { get; set; }
        public string Destination { get; set; }
        public bool Ignore { get; set; }
        public string DataType { get; set; }
    }

    // List-specific configuration that can override global settings
    public class ListConfiguration
    {
        public string Name { get; set; }
        public string SharepointList { get; set; }
        public bool Ignore { get; set; }
        public Dictionary<string, ColumnMapping> Columns { get; set; }
    }
}