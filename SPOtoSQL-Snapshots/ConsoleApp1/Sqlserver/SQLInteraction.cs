using Bring.Sharepoint;
using Bring.XmlConfig;
using Bring.SPODataQuality;
using Microsoft.SharePoint.Client;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Globalization;
using System.Text;

namespace Bring.Sqlserver
{
    /// <summary>
    /// Encapsulates interactions between a SharePoint list and a SQL Server database,
    /// including schema creation, updates, and data transfer with column selection support.
    /// </summary>
    internal class SQLInteraction
    {
        private const string DATE_FORMAT = "yyyy-MM-dd HH:mm:ss.fff";
        private const string FUTURE_DATE = "2100-01-01 00:00:00.000";

        // Core properties
        public SqlConnection Connection { get; set; }
        public SqlCommand Command { get; set; }
        public SqlTransaction Transaction { get; set; }
        public SPOList List { get; set; }
        public string TableName { get; set; }
        public Dictionary<string, Field> FNDictionary { get; set; }
        public string CurrentTime { get; set; }

        // Store selected and ignored columns from configuration
        // private HashSet<string> SelectedColumns { get; set; }
        private HashSet<string> IgnoredColumns { get; set; }
        public Dictionary<string, ColumnMapping> ColumnMappings { get; set; }
        //private Dictionary<string, ColumnMapping> _columnMappings;

        /// <summary>
        /// Initializes the SQL table for the specified SharePoint list,
        /// creating or updating schema as necessary.
        /// </summary>
        public void Build()
        {
            Logger.Log(1, "[Build] Starting SQL build process for list: " + (this.List?.Name ?? "null"));

            try
            {
                // Load selected and ignored columns from configuration
                // this.SelectedColumns = ConfigurationReader.GetSelectedColumns();
                this.IgnoredColumns = ConfigurationReader.GetIgnoredColumns();

                Logger.Log(1, "[Build] Ignored columns from config: " + (this.IgnoredColumns == null ? "None" : string.Join(", ", this.IgnoredColumns)));

                this.TableName = this.ToPascalCase(this.List.Name, false);

                try
                {
                    this.Connection = new SqlConnection(ConfigurationReader.GetSqlConnectionString());
                    Logger.Log(1, "[Build] Establishing SQL connection...");
                    this.Connection.Open();
                }
                catch (SqlException ex)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] SQLInteraction.Build: Database connection failed - {ex.Message}");
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [DEBUG] Stack trace: {ex.StackTrace}");
                    throw;
                }

                InitializeCommandAndTransaction();
                this.CurrentTime = DateTime.Now.ToString(DATE_FORMAT);

                try
                {
                    Logger.Log(1, "[Build] Initializing SharePoint list structure...");
                    this.List.Build();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] SQLInteraction.Build: SharePoint list initialization failed - {ex.Message}");
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [DEBUG] Stack trace: {ex.StackTrace}");
                    throw;
                }

                try
                {
                    this.FNDictionary = new Dictionary<string, Field>(StringComparer.OrdinalIgnoreCase);
                    Logger.Log(1, "[Build] Building field dictionary...");
                    this.BuildDictionary();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] SQLInteraction.Build: Field dictionary creation failed - {ex.Message}");
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [DEBUG] Stack trace: {ex.StackTrace}");
                    throw;
                }

                try
                {
                    if (!this.TableExists(this.TableName))
                    {
                        Logger.Log(1, "[Build] Creating new table: " + this.TableName);
                        this.CreateTable();
                    }
                    else
                    {
                        Logger.Log(1, "[Build] Updating existing table: " + this.TableName);
                        this.UpdateTableDesign();
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

        /// <summary>
        /// Performs a daily update of the SQL table with SharePoint data.
        /// </summary>
        public void DailyUpdate()
        {
            try
            {
                Logger.Log(2, "[DailyUpdate] Starting daily update for table " + this.TableName);

                try
                {
                    this.Command.CommandText = $"DELETE FROM [{this.TableName}] WHERE Snapshot = '{FUTURE_DATE}'";
                    this.Command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] SQLInteraction.DailyUpdate: Failed to delete snapshot marker rows - {ex.Message}");
                }

                try
                {
                    this.TransferData(FUTURE_DATE);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] SQLInteraction.DailyUpdate: Data transfer operation failed - {ex.Message}");
                }

                try
                {
                    this.UpdateMetadata();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] SQLInteraction.DailyUpdate: Metadata update failed - {ex.Message}");
                }

                try
                {
                    this.Transaction.Commit();
                    Logger.Log(2, "[DailyUpdate] Transaction committed successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] SQLInteraction.DailyUpdate: Transaction commit failed - {ex.Message}");
                    throw;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [FATAL] SQLInteraction.DailyUpdate: Critical failure during daily update - {ex.Message}");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [DEBUG] Stack trace: {ex.StackTrace}");
                SafeRollback();
            }
        }

        /// <summary>
        /// Updates the SQL table with current SharePoint data.
        /// </summary>
        public void CurrentTimeUpdate()
        {
            try
            {
                Logger.Log(2, "[CurrentTimeUpdate] Starting current-time update for " + this.TableName);

                try
                {
                    this.TransferData(this.CurrentTime);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] SQLInteraction.CurrentTimeUpdate: Data transfer operation failed - {ex.Message}");
                }

                try
                {
                    this.UpdateMetadata();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] SQLInteraction.CurrentTimeUpdate: Metadata update failed - {ex.Message}");
                }

                try
                {
                    this.Transaction.Commit();
                    Logger.Log(2, "[CurrentTimeUpdate] Transaction committed successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] SQLInteraction.CurrentTimeUpdate: Transaction commit failed - {ex.Message}");
                    throw;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [FATAL] SQLInteraction.CurrentTimeUpdate: Critical failure during current-time update - {ex.Message}");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [DEBUG] Stack trace: {ex.StackTrace}");
                SafeRollback();
            }
        }

        /// <summary>
        /// Builds the dictionary of fields to be replicated, respecting column selection configuration.
        /// </summary>
        private void BuildDictionary()
        {
            Logger.Log(1, "[BuildDictionary] Building field name dictionary...");
            int processedFields = 0;
            int skippedFields = 0;
            int ignoredFields = 0;

            // Use o nome da lista para buscar os campos específicos no UserConfig.xml
            this.ColumnMappings = ConfigurationReader.GetSelectedColumns(this.List.Name);
            this.IgnoredColumns = ConfigurationReader.GetIgnoredColumns();

            foreach (Field field in this.List.Fields)
            {
                if (field.TypeAsString != "Computed")
                {
                    try
                    {
                        string columnName = field.InternalName;

                        // Se houver mapeamento de colunas, só processa as que estão no mapeamento e não estão ignoradas
                        if (this.ColumnMappings != null)
                        {
                            if (this.ColumnMappings.TryGetValue(columnName, out var mapping))
                            {
                                if (mapping.Ignore)
                                {
                                    ignoredFields++;
                                    Logger.Log(1, "[BuildDictionary] Ignored field (by mapping) " + columnName);
                                    continue;
                                }

                                string destinationName = mapping.Destination;
                                this.FNDictionary.Add(this.GetKeyName(destinationName, 1), field);
                                processedFields++;
                                Logger.Log(1, "[BuildDictionary] Added mapped field " + columnName + " -> " + destinationName);
                            }
                            else
                            {
                                skippedFields++;
                                Logger.Log(1, "[BuildDictionary] Skipped field (not mapped) " + columnName);
                            }
                        }
                        // Se não houver mapeamento, processa todos (comportamento padrão)
                        else if (this.IgnoredColumns != null && this.IgnoredColumns.Contains(columnName))
                        {
                            ignoredFields++;
                            Logger.Log(1, "[BuildDictionary] Ignored field (global): " + columnName);
                        }
                        else
                        {
                            this.FNDictionary.Add(this.GetKeyName(columnName, 1), field);
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
                this.Command.CommandText = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{listName}'";
                bool exists = (int)this.Command.ExecuteScalar() != 0;
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
            Logger.Log(1, "[CreateTable] Creating new table: " + this.TableName);
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine($"CREATE TABLE [{this.TableName}] (");
            stringBuilder.AppendLine("[Snapshot] datetime NULL,");

            foreach (var fn in this.FNDictionary)
            {
                string sqlType = null;
                // Verifica se há DataType definido no mapeamento
                if (this.ColumnMappings != null && this.ColumnMappings.TryGetValue(fn.Value.InternalName, out var mapping) && !string.IsNullOrEmpty(mapping.DataType))
                    sqlType = mapping.DataType;
                else
                    sqlType = this.SQLFieldType(fn.Value);

                if (sqlType != null)
                    stringBuilder.AppendLine($"[{fn.Key}] {sqlType} NULL,");
            }

            stringBuilder.Remove(stringBuilder.Length - 3, 3);
            stringBuilder.Append(")");

            this.Command.CommandText = stringBuilder.ToString();
            try
            {
                this.Command.ExecuteNonQuery();
                Logger.Log(1, "[CreateTable] Successfully created table: " + this.TableName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] SQLInteraction.CreateTable: Failed to create table: {this.TableName} - {ex.Message}");
                throw;
            }
        }

        private void UpdateTableDesign()
        {
            Logger.Log(1, "[UpdateTableDesign] Updating design for table: " + this.TableName);
            int updatedColumns = 0;
            int failedColumns = 0;

            foreach (var fn in this.FNDictionary)
            {
                try
                {
                    string sqlType = null;
                    if (this.ColumnMappings != null && this.ColumnMappings.TryGetValue(fn.Value.InternalName, out var mapping) && !string.IsNullOrEmpty(mapping.DataType))
                        sqlType = mapping.DataType;
                    else
                        sqlType = this.SQLFieldType(fn.Value);

                    string colName = fn.Key;

                    this.Command.CommandText = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{this.TableName}' AND COLUMN_NAME = '{colName}'";
                    if ((int)this.Command.ExecuteScalar() == 0)
                    {
                        this.Command.CommandText = $"ALTER TABLE [{this.TableName}] ADD [{colName}] {sqlType} NULL";
                        this.Command.ExecuteNonQuery();
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
            StringBuilder stringBuilder = new StringBuilder();
            string sqlColNames = this.GetSQLColNames();
            int processedItems = 0;
            int failedItems = 0;

            foreach (ListItem listItem in this.List.ItemCollection)
            {
                try
                {
                    stringBuilder.Clear();
                    stringBuilder.AppendLine($"INSERT INTO [{this.TableName}] {sqlColNames}");
                    stringBuilder.Append($"VALUES ('{snapDate}', ");

                    foreach (Field field in this.FNDictionary.Values)
                    {
                        object obj = listItem[field.InternalName];
                        if (obj != null)
                        {
                            if (obj is FieldLookupValue lookup)
                                stringBuilder.Append($"'{lookup.LookupId}', ");
                            else if (obj is FieldUserValue user)
                                stringBuilder.Append($"{user.LookupId}, ");
                            else if (obj is FieldUrlValue url)
                                stringBuilder.Append($"'{url.Url}', ");
                            else if (obj is ContentTypeId)
                                stringBuilder.Append($"'{obj}', ");
                            else if (obj is DateTime dt)
                                stringBuilder.AppendFormat("'{0:" + DATE_FORMAT + "}', ", dt);
                            else if (obj is FieldLookupValue[] lookups)
                            {
                                stringBuilder.Append("'");
                                foreach (var l in lookups) stringBuilder.Append($"{l.LookupId};");
                                stringBuilder.Append("', ");
                            }
                            else if (obj is FieldUserValue[] users)
                            {
                                stringBuilder.Append("'");
                                foreach (var u in users) stringBuilder.Append($"{u.LookupId};");
                                stringBuilder.Append("', ");
                            }
                            else
                            {
                                if (obj is string s) obj = s.Replace("'", "''");
                                stringBuilder.Append($"'{obj}', ");
                            }
                        }
                        else
                            stringBuilder.Append("NULL, ");
                    }

                    stringBuilder.Remove(stringBuilder.Length - 2, 2);
                    stringBuilder.Append(")");
                    this.Command.CommandText = stringBuilder.ToString();

                    try
                    {
                        this.Command.ExecuteNonQuery();
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

        private string SQLFieldType(Field field)
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
            foreach (var fn in this.FNDictionary)
                sb.Append($"[{fn.Key}], ");
            sb.Remove(sb.Length - 2, 2);
            sb.Append(")");
            return sb.ToString();
        }

        private void UpdateMetadata()
        {
            Logger.Log(1, $"[UpdateMetadata] Updating metadata for table: {this.TableName}");
            try
            {
                this.Command.CommandText = $"DELETE FROM Metadata WHERE TableName = '{this.TableName}'";
                this.Command.ExecuteNonQuery();
                this.Command.CommandText = $"INSERT INTO Metadata (TableName, LastRefreshDate) VALUES ('{this.TableName}', '{this.CurrentTime}')";
                this.Command.ExecuteNonQuery();
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
            return this.FNDictionary.ContainsKey(testKey)
                ? this.GetKeyName(key, i + 1)
                : testKey;
        }

        private string GetActualColName(Field field)
        {
            string name = this.ColNameConvetions(field);
            int count = 0;

            foreach (Field f in this.List.Fields)
            {
                if (f.TypeAsString != "Computed" &&
                    name.Equals(this.ColNameConvetions(f), StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count > 1
                ? this.ToPascalCase(field.InternalName, true)
                : name;
        }
        private string ColNameConvetions(Field field)
        {
            var sb = new StringBuilder(this.ToPascalCase(field.Title, false));
            string type = field.TypeAsString;

            if (type == "Choice")
                sb.Append("Value");
            else if (type == "User" || (type == "Lookup" && !field.FromBaseType))
                sb.Append("Id");

            return sb.ToString();
        }

        private string ToPascalCase(string text, bool internalName)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            if (internalName && text.StartsWith("_"))
                text += "IN";

            var sanitized = new StringBuilder();
            foreach (char c in text)
                sanitized.Append(char.IsLetterOrDigit(c) ? c : ' ');

            return CultureInfo.InvariantCulture.TextInfo
                .ToTitleCase(sanitized.ToString())
                .Replace(" ", string.Empty)
                .Replace("X0020", string.Empty)
                .Replace("X003a", string.Empty);
        }

        private void InitializeCommandAndTransaction()
        {
            this.Command = this.Connection.CreateCommand();
            this.Transaction = this.Connection.BeginTransaction($"{this.TableName} TXN");
            this.Command.Connection = this.Connection;
            this.Command.Transaction = this.Transaction;
        }

        private void SafeRollback()
        {
            try
            {
                this.Transaction?.Rollback();
                Logger.Log(1, "[SafeRollback] Transaction rolled back successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] SQLInteraction.SafeRollback: Failed to rollback transaction - {ex.Message}");
            }
        }

        #region Logging Methods

        private void LogInfo(string method, string message)
        {
            Logger.Log(1, $"SQLInteraction.{method}: {message}");
        }

        private void LogError(string method, string message, Exception ex, bool includeStack = false)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] SQLInteraction.{method}: {message} - {ex.Message}");
            if (includeStack)
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [DEBUG] Stack trace: {ex.StackTrace}");
        }

        private void LogWarning(string method, string message)
        {
            Logger.Log(1, $"SQLInteraction.{method}: {message}");
        }

        private void LogDebug(string method, string message)
        {
            Logger.Log(1, $"SQLInteraction.{method}: {message}");
        }

        private void LogFatal(string method, string message, Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [FATAL] SQLInteraction.{method}: {message} - {ex.Message}");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [DEBUG] Stack trace: {ex.StackTrace}");
        }

        private void LogVerbose(string method, string message)
        {
            Logger.Log(1, $"SQLInteraction.{method}: {message}");
        }

        #endregion
    }
}