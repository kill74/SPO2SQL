using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using SPO2SQL.Logging;
using SPO2SQL.Security;

namespace SPO2SQL.XmlConfig;

public static class ConfigurationReader
{
    private static string _configPath = "SPO_to_SQL_config.xml";
    private static readonly object _lock = new object();
    private static XmlDocument _xmlDoc;

    public static void SetConfigPath(string path)
    {
        Logger.LogDebug($"Setting configuration path to: {path}");
        _configPath = path;
        _xmlDoc = null;
    }

    private static void LoadConfig()
    {
        if (_xmlDoc != null)
        {
            return;
        }

        lock (_lock)
        {
            if (_xmlDoc == null)
            {
                try
                {
                    _xmlDoc = new XmlDocument();
                    _xmlDoc.Load(_configPath);
                    Logger.LogDebug($"Configuration file loaded successfully from {_configPath}");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to load configuration file from {_configPath}", ex);
                    throw new Exception($"Error loading configuration file: {ex.Message}", ex);
                }
            }
        }
    }

    public static Dictionary<string, ColumnMapping> GetSelectedColumns(string listName = null)
    {
        LoadConfig();

        try
        {
            if (!string.IsNullOrEmpty(listName))
            {
                var listConfigs = GetListConfigurations();
                if (listConfigs != null && listConfigs.TryGetValue(listName, out var listConfig))
                {
                    if (listConfig.Ignore)
                    {
                        Logger.Log(2, "List " + listName + " is configured to be ignored.");
                        return null;
                    }
                    if (listConfig.Columns != null)
                    {
                        Logger.Log(1, "Using specific configuration for list: " + listName);
                        return listConfig.Columns;
                    }
                }
            }

            var columnNodes = _xmlDoc.SelectNodes("//Configuration/ReplicationConfiguration/SelectColumns/column");

            if (columnNodes == null || columnNodes.Count == 0)
            {
                Logger.Log(2, "No specific columns configured. All columns will be included.");
                return null;
            }

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
                        Destination = destAttr?.Value ?? sourceAttr.Value,
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

    public static HashSet<string> GetIgnoredColumns()
    {
        LoadConfig();

        try
        {
            var columnNodes = _xmlDoc.SelectNodes("//Configuration/ReplicationConfiguration/GlobalIgnore/Column");

            if (columnNodes == null || columnNodes.Count == 0)
            {
                Logger.Log(2, "No columns configured to ignore.");
                return null;
            }

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

    public static (string Username, string Password) GetSharePointCredentials()
    {
        try
        {
            return CredentialManager.GetSharePointCredentials();
        }
        catch
        {
        }

        LoadConfig();
        try
        {
            var spNode = _xmlDoc.SelectSingleNode("//Configuration/SharePoint");
            if (spNode == null)
            {
                throw new InvalidOperationException("SharePoint configuration section not found.");
            }

            var usernameNode = spNode.SelectSingleNode("Username");
            var passwordNode = spNode.SelectSingleNode("Password");

            if (usernameNode == null)
            {
                throw new InvalidOperationException("Username element not found in SharePoint configuration.");
            }

            if (passwordNode == null)
            {
                throw new InvalidOperationException("Password element not found in SharePoint configuration.");
            }

            var username = usernameNode.InnerText.Trim();
            var password = passwordNode.InnerText.Trim();

            if (string.IsNullOrEmpty(username))
            {
                throw new InvalidOperationException("Username cannot be empty.");
            }

            if (string.IsNullOrEmpty(password))
            {
                throw new InvalidOperationException("Password cannot be empty.");
            }

            Logger.Log(2, "SharePoint credentials retrieved successfully from XML config.");
            return (username, password);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error retrieving SharePoint credentials: " + ex.Message);
            throw new InvalidOperationException("Failed to retrieve SharePoint credentials.", ex);
        }
    }

    public static string GetSqlConnectionString()
    {
        try
        {
            return CredentialManager.GetSqlConnectionString();
        }
        catch
        {
        }

        LoadConfig();
        try
        {
            var connNode = _xmlDoc.SelectSingleNode("//Configuration/SQL/ConnectionString");
            if (connNode == null)
            {
                throw new InvalidOperationException("SQL ConnectionString element not found in configuration.");
            }

            string connectionString = connNode.InnerText.Trim();
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("SQL connection string cannot be empty.");
            }

            Logger.Log(2, "SQL connection string retrieved successfully from XML config.");
            return connectionString;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error retrieving SQL connection string: " + ex.Message);
            throw new InvalidOperationException("Failed to retrieve SQL connection string.", ex);
        }
    }

    public static bool IsTwoFactorEnabled()
    {
        LoadConfig();

        try
        {
            var secNode = _xmlDoc.SelectSingleNode("//Configuration/Security");
            if (secNode == null)
            {
                return false;
            }

            var enabledNode = secNode.SelectSingleNode("TwoFactorEnabled");
            if (enabledNode == null)
            {
                return false;
            }

            return bool.TryParse(enabledNode.InnerText.Trim(), out bool result) && result;
        }
        catch
        {
            return false;
        }
    }

    public static string GetTwoFactorSecret()
    {
        LoadConfig();

        try
        {
            var secNode = _xmlDoc.SelectSingleNode("//Configuration/Security");
            return secNode?.SelectSingleNode("TwoFactorSecret")?.InnerText.Trim() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public static string GetSharePointBaseUrl()
    {
        LoadConfig();

        try
        {
            var spNode = _xmlDoc.SelectSingleNode("//Configuration/SharePoint");
            if (spNode == null)
            {
                return null;
            }

            var baseUrlNode = spNode.SelectSingleNode("BaseUrl");
            if (baseUrlNode == null)
            {
                Logger.LogDebug("SharePoint BaseUrl not configured in XML, using default.");
                return null;
            }

            string baseUrl = baseUrlNode.InnerText.Trim();
            if (string.IsNullOrEmpty(baseUrl))
            {
                Logger.LogDebug("SharePoint BaseUrl is empty, using default.");
                return null;
            }

            Logger.LogDebug($"SharePoint base URL retrieved: {baseUrl}");
            return baseUrl;
        }
        catch (Exception ex)
        {
            Logger.LogDebug($"Error retrieving SharePoint base URL: {ex.Message}");
            return null;
        }
    }

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

    private static string GetNodeValue(XmlNode parentNode, string nodeName)
    {
        var node = parentNode.SelectSingleNode(nodeName);
        return node?.InnerText.Trim() ?? string.Empty;
    }

    private static bool GetNodeValueBool(XmlNode parentNode, string nodeName, bool defaultValue)
    {
        var value = GetNodeValue(parentNode, nodeName);
        return !string.IsNullOrEmpty(value) && bool.TryParse(value, out bool result)
            ? result
            : defaultValue;
    }

    private static bool IsValidListConfig(SharePointListConfig config)
    {
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
            {
                return null;
            }

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
                        Columns = GetListColumns(listNode)
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
        {
            return null;
        }

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

public class SharePointListConfig
{
    public string SiteUrl { get; set; }
    public string ListTitle { get; set; }
    public string SqlTable { get; set; }
    public bool AutoAddNewColumns { get; set; }
    public bool Disabled { get; set; }
}

public class ColumnMapping
{
    public string Source { get; set; }
    public string Destination { get; set; }
    public bool Ignore { get; set; }
    public string DataType { get; set; }
}

public class ListConfiguration
{
    public string Name { get; set; }
    public string SharepointList { get; set; }
    public bool Ignore { get; set; }
    public Dictionary<string, ColumnMapping> Columns { get; set; }
}
