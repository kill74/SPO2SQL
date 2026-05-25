using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.SharePoint.Client;
using SPO2SQL.Logging;
using SPO2SQL.SharePoint;
using SPO2SQL.XmlConfig;

namespace SPO2SQL.SqlServer;

internal sealed class SQLInteraction : IDisposable
{
    private const string DATE_FORMAT = "yyyy-MM-dd HH:mm:ss.fff";
    private const string FUTURE_DATE = "2100-01-01 00:00:00.000";

    private static readonly HashSet<string> ValidSqlTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "[bit]", "[sql_variant]", "[nvarchar](MAX)", "[varbinary](MAX)",
        "[int]", "[float]", "[datetime]", "[uniqueidentifier]",
        "[nvarchar]", "[varchar]", "[nchar]", "[char]",
        "[bigint]", "[smallint]", "[tinyint]", "[decimal]", "[numeric]",
        "[money]", "[smallmoney]", "[real]", "[date]", "[datetime2]",
        "[datetimeoffset]", "[time]", "[text]", "[ntext]", "[image]",
        "[binary]", "[varbinary]", "[xml]"
    };

    private bool _transactionCommitted;

    public SqlConnection Connection { get; private set; }
    public SqlCommand Command { get; private set; }
    public SqlTransaction Transaction { get; private set; }
    public SPOList List { get; set; }
    public string TableName { get; private set; }
    public Dictionary<string, Field> FNDictionary { get; private set; }
    public string CurrentTime { get; private set; }

    public bool DailyMode { get; set; }
    public int CommandTimeoutSeconds { get; set; } = 300;

    private HashSet<string> IgnoredColumns { get; set; }
    public Dictionary<string, ColumnMapping> ColumnMappings { get; private set; }

    private static string SanitizeSqlType(string dataType)
    {
        if (dataType != null && ValidSqlTypes.Contains(dataType.Trim()))
        {
            return dataType.Trim();
        }

        return null;
    }

    public void Dispose()
    {
        if (!_transactionCommitted)
        {
            SafeRollback();
        }

        Command?.Dispose();
        Connection?.Close();
        Connection?.Dispose();
    }

    public void Build()
    {
        Logger.Log(1, "[Build] Starting SQL build process for list: " + (List?.Name ?? "null"));

        try
        {
            IgnoredColumns = ConfigurationReader.GetIgnoredColumns();

            Logger.Log(1, "[Build] Ignored columns from config: " + (IgnoredColumns == null ? "None" : string.Join(", ", IgnoredColumns)));

            TableName = ToPascalCase(List.Name, false);

            try
            {
                Connection = new SqlConnection(ConfigurationReader.GetSqlConnectionString());
                Logger.Log(1, "[Build] Establishing SQL connection...");
                Connection.Open();
            }
            catch (SqlException ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] SQLInteraction.Build: Database connection failed - {ex.Message}");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [DEBUG] Stack trace: {ex.StackTrace}");
                throw;
            }

            InitializeCommandAndTransaction();
            CurrentTime = DateTime.Now.ToString(DATE_FORMAT);

            if (DailyMode && TableExists(TableName))
            {
                string lastSync = GetLastSyncDate();
                if (lastSync != null)
                {
                    Logger.Log(1, "[Build] Applying incremental filter: Modified >= " + lastSync);
                    List.CAMLQuery = CamlQueryBuilder.BuildDateRangeQuery("Modified",
                        DateTime.Parse(lastSync), DateTime.Now);
                }
            }

            try
            {
                Logger.Log(1, "[Build] Initializing SharePoint list structure...");
                List.Build();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] SQLInteraction.Build: SharePoint list initialization failed - {ex.Message}");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [DEBUG] Stack trace: {ex.StackTrace}");
                throw;
            }

            try
            {
                FNDictionary = new Dictionary<string, Field>(StringComparer.OrdinalIgnoreCase);
                Logger.Log(1, "[Build] Building field dictionary...");
                BuildDictionary();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] SQLInteraction.Build: Field dictionary creation failed - {ex.Message}");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [DEBUG] Stack trace: {ex.StackTrace}");
                throw;
            }

            try
            {
                if (!TableExists(TableName))
                {
                    Logger.Log(1, "[Build] Creating new table: " + TableName);
                    CreateTable();
                }
                else
                {
                    Logger.Log(1, "[Build] Updating existing table: " + TableName);
                    UpdateTableDesign();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] SQLInteraction.Build: Table structure operation failed - {ex.Message}");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [DEBUG] Stack trace: {ex.StackTrace}");
                throw;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [FATAL] SQLInteraction.Build: Critical failure during build process - {ex.Message}");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [DEBUG] Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    public void DailyUpdate()
    {
        try
        {
            Logger.Log(2, "[DailyUpdate] Starting daily update for table " + TableName);

            Command.CommandText = $"DELETE FROM [{TableName.Replace("]", "]]")}] WHERE Snapshot = @FutureDate";
            Command.Parameters.Clear();
            Command.Parameters.AddWithValue("@FutureDate", FUTURE_DATE);
            Command.ExecuteNonQuery();

            TransferData(FUTURE_DATE);

            UpdateMetadata();

            Transaction.Commit();
            _transactionCommitted = true;
            Logger.Log(2, "[DailyUpdate] Transaction committed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [FATAL] SQLInteraction.DailyUpdate: Critical failure during daily update - {ex.Message}");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [DEBUG] Stack trace: {ex.StackTrace}");
            SafeRollback();
            throw;
        }
    }

    public void CurrentTimeUpdate()
    {
        try
        {
            Logger.Log(2, "[CurrentTimeUpdate] Starting current-time update for " + TableName);

            TransferData(CurrentTime);

            UpdateMetadata();

            Transaction.Commit();
            _transactionCommitted = true;
            Logger.Log(2, "[CurrentTimeUpdate] Transaction committed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [FATAL] SQLInteraction.CurrentTimeUpdate: Critical failure during current-time update - {ex.Message}");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [DEBUG] Stack trace: {ex.StackTrace}");
            SafeRollback();
            throw;
        }
    }

    private void BuildDictionary()
    {
        Logger.Log(1, "[BuildDictionary] Building field name dictionary...");
        int processedFields = 0;
        int skippedFields = 0;
        int ignoredFields = 0;

        ColumnMappings = ConfigurationReader.GetSelectedColumns(List.Name);
        IgnoredColumns = ConfigurationReader.GetIgnoredColumns();

        foreach (Field field in List.Fields)
        {
            if (field.TypeAsString != "Computed")
            {
                try
                {
                    string columnName = field.InternalName;

                    if (ColumnMappings != null)
                    {
                        if (ColumnMappings.TryGetValue(columnName, out var mapping))
                        {
                            if (mapping.Ignore)
                            {
                                ignoredFields++;
                                Logger.Log(1, "[BuildDictionary] Ignored field (by mapping) " + columnName);
                                continue;
                            }

                            string destinationName = mapping.Destination;
                            FNDictionary.Add(GetKeyName(destinationName, 1), field);
                            processedFields++;
                            Logger.Log(1, "[BuildDictionary] Added mapped field " + columnName + " -> " + destinationName);
                        }
                        else
                        {
                            skippedFields++;
                            Logger.Log(1, "[BuildDictionary] Skipped field (not mapped) " + columnName);
                        }
                    }
                    else if (IgnoredColumns != null && IgnoredColumns.Contains(columnName))
                    {
                        ignoredFields++;
                        Logger.Log(1, "[BuildDictionary] Ignored field (global): " + columnName);
                    }
                    else
                    {
                        FNDictionary.Add(GetKeyName(columnName, 1), field);
                        processedFields++;
                        Logger.Log(1, "[BuildDictionary] Added field: " + columnName);
                    }
                }
                catch (Exception ex)
                {
                    skippedFields++;
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] SQLInteraction.BuildDictionary: Failed to process field: {field.Title} - {ex.Message}");
                }
            }
        }

        Logger.Log(1, "[BuildDictionary] Dictionary built. Processed: " + processedFields + ", Skipped: " + skippedFields + ", Ignored: " + ignoredFields);
    }

    private bool TableExists(string listName)
    {
        try
        {
            Command.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @TableName";
            Command.Parameters.Clear();
            Command.Parameters.AddWithValue("@TableName", listName);
            bool exists = (int)Command.ExecuteScalar() != 0;
            Logger.Log(1, $"[TableExists] Table '{listName}' exists: {exists}");
            return exists;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] SQLInteraction.TableExists: Failed to check existence of table '{listName}' - {ex.Message}");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [DEBUG] Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    private void CreateTable()
    {
        Logger.Log(1, "[CreateTable] Creating new table: " + TableName);
        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.AppendLine($"CREATE TABLE [{TableName.Replace("]", "]]")}] (");
        stringBuilder.AppendLine("[Snapshot] datetime NULL,");

        foreach (var fn in FNDictionary)
        {
            string sqlType = null;
            if (ColumnMappings != null && ColumnMappings.TryGetValue(fn.Value.InternalName, out var mapping) && !string.IsNullOrEmpty(mapping.DataType))
            {
                sqlType = SanitizeSqlType(mapping.DataType) ?? SQLFieldType(fn.Value);
            }
            else
            {
                sqlType = SQLFieldType(fn.Value);
            }

            if (sqlType != null)
            {
                stringBuilder.AppendLine($"[{fn.Key}] {sqlType} NULL,");
            }
        }

        stringBuilder.Remove(stringBuilder.Length - 3, 3);
        stringBuilder.Append(')');

        Command.CommandText = stringBuilder.ToString();
        try
        {
            Command.ExecuteNonQuery();
            Logger.Log(1, "[CreateTable] Successfully created table: " + TableName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] SQLInteraction.CreateTable: Failed to create table: {TableName} - {ex.Message}");
            throw;
        }
    }

    private void UpdateTableDesign()
    {
        Logger.Log(1, "[UpdateTableDesign] Updating design for table: " + TableName);
        int updatedColumns = 0;
        int failedColumns = 0;

        foreach (var fn in FNDictionary)
        {
            try
            {
                string sqlType = null;
                if (ColumnMappings != null && ColumnMappings.TryGetValue(fn.Value.InternalName, out var mapping) && !string.IsNullOrEmpty(mapping.DataType))
                {
                    sqlType = SanitizeSqlType(mapping.DataType) ?? SQLFieldType(fn.Value);
                }
                else
                {
                    sqlType = SQLFieldType(fn.Value);
                }

                if (sqlType == null)
                {
                    failedColumns++;
                    continue;
                }

                string colName = fn.Key;

                Command.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @TableName AND COLUMN_NAME = @ColName";
                Command.Parameters.Clear();
                Command.Parameters.AddWithValue("@TableName", TableName);
                Command.Parameters.AddWithValue("@ColName", colName);
                if ((int)Command.ExecuteScalar() == 0)
                {
                    string safeTable = $"[{TableName.Replace("]", "]]")}]";
                    string safeCol = $"[{colName.Replace("]", "]]")}]";
                    Command.CommandText = $"ALTER TABLE {safeTable} ADD {safeCol} {sqlType} NULL";
                    Command.Parameters.Clear();
                    Command.ExecuteNonQuery();
                    updatedColumns++;
                }
            }
            catch (Exception ex)
            {
                failedColumns++;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] SQLInteraction.UpdateTableDesign: Failed to update column: {fn.Key} - {ex.Message}");
            }
        }

        Logger.Log(1, "[UpdateTableDesign] Design update completed. Updated: " + updatedColumns + ", Failed: " + failedColumns);
    }

    private void TransferData(string snapDate)
    {
        Logger.Log(1, "[TransferData] Beginning data transfer for snapshot: " + snapDate);
        string sqlColNames = GetSQLColNames();
        int processedItems = 0;
        int failedItems = 0;

        string safeTable = $"[{TableName.Replace("]", "]]")}]";
        string insertBase = $"INSERT INTO {safeTable} {sqlColNames} VALUES (@Snapshot";

        var fieldList = new List<Field>(FNDictionary.Values);
        for (int i = 0; i < fieldList.Count; i++)
        {
            insertBase += $", @F{i}";
        }

        insertBase += ")";

        foreach (ListItem listItem in List.ItemCollection)
        {
            try
            {
                Command.CommandText = insertBase;
                Command.Parameters.Clear();
                Command.Parameters.AddWithValue("@Snapshot", snapDate);

                int idx = 0;
                foreach (Field field in fieldList)
                {
                    object obj;
                    try
                    { obj = listItem[field.InternalName]; }
                    catch { obj = null; }
                    string paramName = $"@F{idx}";

                    if (obj != null)
                    {
                        if (obj is FieldLookupValue lookup)
                        {
                            Command.Parameters.AddWithValue(paramName, lookup.LookupId);
                        }
                        else if (obj is FieldUserValue user)
                        {
                            Command.Parameters.AddWithValue(paramName, user.LookupId);
                        }
                        else if (obj is FieldUrlValue url)
                        {
                            Command.Parameters.AddWithValue(paramName, (object)url.Url ?? DBNull.Value);
                        }
                        else if (obj is ContentTypeId ctId)
                        {
                            Command.Parameters.AddWithValue(paramName, ctId.StringValue);
                        }
                        else if (obj is DateTime dt)
                        {
                            Command.Parameters.AddWithValue(paramName, dt);
                        }
                        else if (obj is FieldLookupValue[] lookups)
                        {
                            Command.Parameters.AddWithValue(paramName, lookups.Length > 0 ? (object)string.Join(";", lookups.Select(l => l.LookupId)) : DBNull.Value);
                        }
                        else if (obj is FieldUserValue[] users)
                        {
                            Command.Parameters.AddWithValue(paramName, users.Length > 0 ? (object)string.Join(";", users.Select(u => u.LookupId)) : DBNull.Value);
                        }
                        else
                        {
                            Command.Parameters.AddWithValue(paramName, obj);
                        }
                    }
                    else
                    {
                        Command.Parameters.AddWithValue(paramName, DBNull.Value);
                    }

                    idx++;
                }

                try
                {
                    Command.ExecuteNonQuery();
                    processedItems++;
                }
                catch (Exception ex)
                {
                    failedItems++;
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] SQLInteraction.TransferData: Failed to insert item {processedItems + failedItems} - {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                failedItems++;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] SQLInteraction.TransferData: Failed to process item {processedItems + failedItems} - {ex.Message}");
            }
        }

        Logger.Log(1, $"[TransferData] Transfer completed. Processed: {processedItems}, Failed: {failedItems}");
    }

    private static string SQLFieldType(Field field)
    {
        switch (field.TypeAsString)
        {
            case "Attachments":
            case "Boolean":
                return "[bit]";
            case "Calculated":
                return "[sql_variant]";
            case "Choice":
            case "File":
            case "LookupMulti":
            case "Note":
            case "Text":
            case "URL":
            case "UserMulti":
                return "[nvarchar](MAX)";
            case "ContentTypeId":
                return "[varbinary](MAX)";
            case "Counter":
            case "Integer":
            case "ModStat":
            case "User":
                return "[int]";
            case "Currency":
            case "Number":
                return "[float]";
            case "DateTime":
                return "[datetime]";
            case "Guid":
                return "[uniqueidentifier]";
            case "Lookup":
                return field.FromBaseType
                    ? "[nvarchar](MAX)"
                    : "[int]";
            default:
                Logger.Log(1, $"[SQLFieldType] Unknown field type encountered - Field: {field.Title}, Type: {field.TypeAsString}");
                return null;
        }
    }

    private string GetSQLColNames()
    {
        var sb = new StringBuilder();
        sb.Append("([Snapshot], ");
        foreach (var fn in FNDictionary)
        {
            sb.Append($"[{fn.Key}], ");
        }

        sb.Remove(sb.Length - 2, 2);
        sb.Append(')');
        return sb.ToString();
    }

    private string GetLastSyncDate()
    {
        try
        {
            Command.CommandText = "SELECT LastRefreshDate FROM Metadata WHERE TableName = @TableName";
            Command.Parameters.Clear();
            Command.Parameters.AddWithValue("@TableName", TableName);
            object result = Command.ExecuteScalar();

            if (result == null || result == DBNull.Value)
            {
                return null;
            }

            if (result is DateTime dt)
            {
                return dt.ToString(DATE_FORMAT);
            }

            string str = result.ToString();
            return string.IsNullOrEmpty(str) ? null : str;
        }
        catch
        {
            return null;
        }
    }

    private void UpdateMetadata()
    {
        Logger.Log(1, $"[UpdateMetadata] Updating metadata for table: {TableName}");
        try
        {
            Command.CommandText = "DELETE FROM Metadata WHERE TableName = @TableName";
            Command.Parameters.Clear();
            Command.Parameters.AddWithValue("@TableName", TableName);
            Command.ExecuteNonQuery();
            Command.CommandText = "INSERT INTO Metadata (TableName, LastRefreshDate) VALUES (@TableName, @CurrentTime)";
            Command.Parameters.Clear();
            Command.Parameters.AddWithValue("@TableName", TableName);
            Command.Parameters.AddWithValue("@CurrentTime", CurrentTime);
            Command.ExecuteNonQuery();
            Logger.Log(1, $"[UpdateMetadata] Metadata updated successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] SQLInteraction.UpdateMetadata: Failed to update metadata - {ex.Message}");
            throw;
        }
    }

    private string GetKeyName(string key, int i = 1)
    {
        string testKey = i == 1 ? key : $"{key}{i}";
        return FNDictionary.ContainsKey(testKey)
            ? GetKeyName(key, i + 1)
            : testKey;
    }

    private string GetActualColName(Field field)
    {
        string name = ColNameConvetions(field);
        int count = 0;

        foreach (Field f in List.Fields)
        {
            if (f.TypeAsString != "Computed" &&
                name.Equals(ColNameConvetions(f), StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }

        return count > 1
            ? ToPascalCase(field.InternalName, true)
            : name;
    }
    private static string ColNameConvetions(Field field)
    {
        var sb = new StringBuilder(ToPascalCase(field.Title, false));
        string type = field.TypeAsString;

        if (type == "Choice")
        {
            sb.Append("Value");
        }
        else if (type == "User" || (type == "Lookup" && !field.FromBaseType))
        {
            sb.Append("Id");
        }

        return sb.ToString();
    }

    private static string ToPascalCase(string text, bool internalName)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        if (internalName && text.StartsWith("_"))
        {
            text += "IN";
        }

        var sanitized = new StringBuilder();
        foreach (char c in text)
        {
            sanitized.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }

        return CultureInfo.InvariantCulture.TextInfo
            .ToTitleCase(sanitized.ToString())
            .Replace(" ", string.Empty)
            .Replace("X0020", string.Empty)
            .Replace("X003a", string.Empty);
    }

    private void InitializeCommandAndTransaction()
    {
        Command = Connection.CreateCommand();
        Command.CommandTimeout = CommandTimeoutSeconds;
        Transaction = Connection.BeginTransaction($"{TableName} TXN");
        Command.Connection = Connection;
        Command.Transaction = Transaction;
    }

    private void SafeRollback()
    {
        try
        {
            Transaction?.Rollback();
            Logger.Log(1, "[SafeRollback] Transaction rolled back successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] SQLInteraction.SafeRollback: Failed to rollback transaction - {ex.Message}");
        }
    }

    #region Logging Methods

    private static void LogInfo(string method, string message)
    {
        Logger.Log(1, $"SQLInteraction.{method}: {message}");
    }

    private static void LogError(string method, string message, Exception ex, bool includeStack = false)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] SQLInteraction.{method}: {message} - {ex.Message}");
        if (includeStack)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [DEBUG] Stack trace: {ex.StackTrace}");
        }
    }

    private static void LogWarning(string method, string message)
    {
        Logger.Log(1, $"SQLInteraction.{method}: {message}");
    }

    private static void LogDebug(string method, string message)
    {
        Logger.Log(1, $"SQLInteraction.{method}: {message}");
    }

    private static void LogFatal(string method, string message, Exception ex)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [FATAL] SQLInteraction.{method}: {message} - {ex.Message}");
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [DEBUG] Stack trace: {ex.StackTrace}");
    }

    private static void LogVerbose(string method, string message)
    {
        Logger.Log(1, $"SQLInteraction.{method}: {message}");
    }

    #endregion
}
