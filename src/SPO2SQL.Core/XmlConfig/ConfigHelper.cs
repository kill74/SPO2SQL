using System.Xml;
using SPO2SQL.Logging;
using SPO2SQL.Security;

namespace SPO2SQL.XmlConfig;

/// <summary>
/// Reads the legacy XML file used for list mappings. Credentials are intentionally
/// not read from here; the credential provider chain is the only secret source.
/// </summary>
public static class ConfigurationReader
{
    private static string _configPath = "SPO_to_SQL_config.xml";
    private static string? _sharePointBaseUrlOverride;
    private static readonly object Lock = new();
    private static XmlDocument? _xmlDoc;

    public static void SetConfigPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        Logger.LogDebug($"Setting configuration path to: {path}");
        lock (Lock)
        {
            _configPath = path;
            _xmlDoc = null;
        }
    }

    public static void SetSharePointBaseUrl(string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new ArgumentException("SharePoint base URL must be an absolute HTTP or HTTPS URL.", nameof(baseUrl));
        }

        lock (Lock)
        {
            _sharePointBaseUrlOverride = uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
        }
    }

    private static bool ParseBoolean(string? value, bool defaultValue) =>
        bool.TryParse(value, out bool result) ? result : defaultValue;

    private static XmlDocument LoadConfig()
    {
        XmlDocument? document = Volatile.Read(ref _xmlDoc);
        if (document != null)
        {
            return document;
        }

        lock (Lock)
        {
            if (_xmlDoc == null)
            {
                try
                {
                    var settings = new XmlReaderSettings
                    {
                        DtdProcessing = DtdProcessing.Prohibit,
                        XmlResolver = null
                    };

                    var loaded = new XmlDocument { XmlResolver = null };
                    using var reader = XmlReader.Create(_configPath, settings);
                    loaded.Load(reader);
                    _xmlDoc = loaded;
                    Logger.LogDebug($"Configuration file loaded successfully from {_configPath}");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to load configuration file from {_configPath}", ex);
                    throw new InvalidOperationException($"Error loading configuration file: {ex.Message}", ex);
                }
            }

            return _xmlDoc;
        }
    }

    public static Dictionary<string, ColumnMapping>? GetSelectedColumns(string? listName = null)
    {
        XmlDocument document = LoadConfig();

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

            var columnNodes = document.SelectNodes("//Configuration/ReplicationConfiguration/SelectColumns/column");

            if (columnNodes == null || columnNodes.Count == 0)
            {
                Logger.Log(2, "No specific columns configured. All columns will be included.");
                return null;
            }

            var columnMappings = new Dictionary<string, ColumnMapping>(StringComparer.OrdinalIgnoreCase);

            foreach (XmlNode node in columnNodes)
            {
                var sourceAttr = node.Attributes?["source"];
                if (sourceAttr == null)
                {
                    continue;
                }

                var destAttr = node.Attributes?["destination"];
                var ignoreAttr = node.Attributes?["ignore"];

                var mapping = new ColumnMapping
                {
                    Source = sourceAttr.Value,
                    Destination = destAttr?.Value ?? sourceAttr.Value,
                    Ignore = ParseBoolean(ignoreAttr?.Value, false)
                };

                columnMappings[mapping.Source] = mapping;
                Logger.Log(1, $"Added column mapping: {mapping.Source} -> {mapping.Destination} (Ignore: {mapping.Ignore})");
            }

            return columnMappings.Count > 0 ? columnMappings : null;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            Logger.LogError("Error reading selected columns configuration", ex);
            throw new InvalidOperationException("Error reading selected columns configuration.", ex);
        }
    }

    public static HashSet<string>? GetIgnoredColumns()
    {
        XmlDocument document = LoadConfig();

        try
        {
            var columnNodes = document.SelectNodes("//Configuration/ReplicationConfiguration/GlobalIgnore/Column");

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
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            Logger.LogError("Error reading ignored columns configuration", ex);
            throw new InvalidOperationException("Error reading ignored columns configuration.", ex);
        }
    }

    public static (string Username, string Password) GetSharePointCredentials() =>
        CredentialManager.GetSharePointCredentials();

    public static string GetSqlConnectionString() =>
        CredentialManager.GetSqlConnectionString();

    public static string? GetSharePointBaseUrl()
    {
        lock (Lock)
        {
            if (!string.IsNullOrEmpty(_sharePointBaseUrlOverride))
            {
                return _sharePointBaseUrlOverride;
            }
        }

        try
        {
            XmlDocument document = LoadConfig();
            var spNode = document.SelectSingleNode("//Configuration/SharePoint");
            var baseUrlNode = spNode?.SelectSingleNode("BaseUrl");
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
        XmlDocument document = LoadConfig();

        try
        {
            var listNodes = document.SelectNodes("//Configuration/ReplicationConfiguration/SharePointLists/List");
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
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            Logger.LogError("Error reading SharePoint list configurations", ex);
            throw new InvalidOperationException("Failed to read SharePoint list configurations.", ex);
        }
    }

    private static string GetNodeValue(XmlNode parentNode, string nodeName)
    {
        var node = parentNode.SelectSingleNode(nodeName);
        return node?.InnerText.Trim() ?? string.Empty;
    }

    private static bool GetNodeValueBool(XmlNode parentNode, string nodeName, bool defaultValue)
    {
        string value = GetNodeValue(parentNode, nodeName);
        return bool.TryParse(value, out bool result) ? result : defaultValue;
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

    public static Dictionary<string, ListConfiguration>? GetListConfigurations()
    {
        XmlDocument document = LoadConfig();

        try
        {
            var listNodes = document.SelectNodes("//Configuration/ReplicationConfiguration/Lists/List");
            if (listNodes == null || listNodes.Count == 0)
            {
                return null;
            }

            var listConfigs = new Dictionary<string, ListConfiguration>(StringComparer.OrdinalIgnoreCase);

            foreach (XmlNode listNode in listNodes)
            {
                var nameAttr = listNode.Attributes?["name"];
                if (nameAttr == null)
                {
                    continue;
                }

                var contextAttr = listNode.Attributes?["sharepointlist"];
                var ignoreAttr = listNode.Attributes?["ignore"];

                var listConfig = new ListConfiguration
                {
                    Name = nameAttr.Value,
                    SharepointList = contextAttr?.Value ?? string.Empty,
                    Ignore = ParseBoolean(ignoreAttr?.Value, false),
                    Columns = GetListColumns(listNode)
                };

                listConfigs[listConfig.Name] = listConfig;
            }

            return listConfigs.Count > 0 ? listConfigs : null;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            Logger.LogError("Error reading list configurations", ex);
            throw new InvalidOperationException("Error reading list configurations.", ex);
        }
    }

    private static Dictionary<string, ColumnMapping>? GetListColumns(XmlNode listNode)
    {
        var columnNodes = listNode.SelectNodes(".//Columns/column");
        if (columnNodes == null || columnNodes.Count == 0)
        {
            return null;
        }

        var columnMappings = new Dictionary<string, ColumnMapping>(StringComparer.OrdinalIgnoreCase);

        foreach (XmlNode node in columnNodes)
        {
            var sourceAttr = node.Attributes?["source"];
            if (sourceAttr == null)
            {
                continue;
            }

            var destAttr = node.Attributes?["destination"];
            var ignoreAttr = node.Attributes?["ignore"];
            var datatypeAttr = node.Attributes?["datatype"];

            var mapping = new ColumnMapping
            {
                Source = sourceAttr.Value,
                Destination = destAttr?.Value ?? sourceAttr.Value,
                Ignore = ParseBoolean(ignoreAttr?.Value, false),
                DataType = datatypeAttr?.Value
            };

            columnMappings[mapping.Source] = mapping;
        }

        return columnMappings.Count > 0 ? columnMappings : null;
    }
}

public sealed class SharePointListConfig
{
    public string SiteUrl { get; set; } = string.Empty;
    public string ListTitle { get; set; } = string.Empty;
    public string SqlTable { get; set; } = string.Empty;
    public bool AutoAddNewColumns { get; set; }
    public bool Disabled { get; set; }
}

public sealed class ColumnMapping
{
    public string Source { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public bool Ignore { get; set; }
    public string? DataType { get; set; }
}

public sealed class ListConfiguration
{
    public string Name { get; set; } = string.Empty;
    public string SharepointList { get; set; } = string.Empty;
    public bool Ignore { get; set; }
    public Dictionary<string, ColumnMapping>? Columns { get; set; }
}
