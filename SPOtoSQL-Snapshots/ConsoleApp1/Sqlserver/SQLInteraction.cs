using Bring.Sharepoint;
using Bring.XmlConfig;
using Bring.SPODataQuality;
using Microsoft.SharePoint.Client;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.Data.SqlClient;

namespace Bring.Sqlserver
{
    internal class SQLInteraction : IDisposable
    {
        private const string DATE_FORMAT = "yyyy-MM-dd HH:mm:ss.fff";
        private const string FUTURE_DATE = "2100-01-01 00:00:00.000";

        public SqlConnection Connection { get; set; }
        public SqlCommand Command { get; set; }
        public SqlTransaction Transaction { get; set; }
        public SPOList List { get; set; }
        public string TableName { get; set; }
        public Dictionary<string, Field> FNDictionary { get; set; }
        public string CurrentTime { get; set; }

        public bool DailyMode { get; set; }
        public int CommandTimeoutSeconds { get; set; } = 300;

        private HashSet<string> IgnoredColumns { get; set; }
        public Dictionary<string, ColumnMapping> ColumnMappings { get; set; }

        private bool? _tableExistsCache;

        public void Dispose()
        {
            SafeRollback();
            Command?.Dispose();
            Connection?.Close();
            Connection?.Dispose();
            _tableExistsCache = null;
        }

        /// <summary>
        /// Initializes the SQL table for the specified SharePoint list,
        /// creating or updating schema as necessary.
        /// </summary>
        public void Build()
        {
            Logger.Log(1, "[Build] Starting SQL build process for list: " + (this.List?.Name ?? "null"));

            try
            {
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

                bool tableAlreadyExists = this.TableExists(this.TableName);

                if (this.DailyMode && tableAlreadyExists)
                {
                    string lastSync = GetLastSyncDate();
                    if (lastSync != null)
                    {
                        Logger.Log(1, "[Build] Applying incremental filter: Modified >= " + lastSync);
                        this.List.CAMLQuery = CamlQueryBuilder.BuildDateRangeQuery("Modified",
                            DateTime.Parse(lastSync), DateTime.Now);
                    }
                }

                try
                {
                    Logger.Log(1, "[Build] Initializing SharePoint list structure...");
                    this.List.Build(pageSize: 100);
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
                    if (!tableAlreadyExists)
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
                    SafeRollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [FATAL] SQLInteraction.Build: Critical failure during build process - {ex.Message}");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [DEBUG] Stack trace: {ex.StackTrace}");
                SafeRollback();
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

                this.Command.CommandText = $"DELETE FROM [{this.TableName.Replace("]", "]]")}] WHERE Snapshot = @FutureDate";
                this.Command.Parameters.Clear();
                this.Command.Parameters.AddWithValue("@FutureDate", FUTURE_DATE);
                this.Command.ExecuteNonQuery();

                this.TransferData(FUTURE_DATE);

                this.UpdateMetadata();

                this.Transaction.Commit();
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
                Logger.Log(2, "[CurrentTimeUpdate] Starting current-time update for " + this.TableName);

                this.TransferData(this.CurrentTime);

                this.UpdateMetadata();

                this.Transaction.Commit();
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

            this.ColumnMappings = ConfigurationReader.GetSelectedColumns(this.List.Name);

            foreach (Field field in this.List.Fields)
            {
                if (field.TypeAsString != "Computed")
                {
                    try
                    {
                        string columnName = field.InternalName;

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
            if (_tableExistsCache.HasValue)
                return _tableExistsCache.Value;

            try
            {
                this.Command.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @TableName";
                this.Command.Parameters.Clear();
                this.Command.Parameters.AddWithValue("@TableName", listName);
                bool exists = (int)this.Command.ExecuteScalar() != 0;
                _tableExistsCache = exists;
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
            var sb = new StringBuilder();
            sb.AppendLine($"CREATE TABLE [{this.TableName}] (");
            sb.AppendLine("[Snapshot] datetime NULL,");

            foreach (var fn in this.FNDictionary)
            {
                string sqlType = ResolveSqlType(fn.Value);
                if (sqlType != null)
                    sb.AppendLine($"[{fn.Key}] {sqlType} NULL,");
            }

            sb.Remove(sb.Length - 3, 3);
            sb.Append(")");

            this.Command.CommandText = sb.ToString();
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

            // Fetch existing columns once
            var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                this.Command.CommandText = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @TableName";
                this.Command.Parameters.Clear();
                this.Command.Parameters.AddWithValue("@TableName", this.TableName);
                using var reader = this.Command.ExecuteReader();
                while (reader.Read())
                    existingColumns.Add(reader.GetString(0));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] SQLInteraction.UpdateTableDesign: Failed to read existing columns - {ex.Message}");
                throw;
            }

            int updatedColumns = 0;
            int failedColumns = 0;

            foreach (var fn in this.FNDictionary)
            {
                if (existingColumns.Contains(fn.Key)) continue;

                try
                {
                    string sqlType = ResolveSqlType(fn.Value);
                    if (sqlType == null) continue;

                    string safeTable = $"[{this.TableName.Replace("]", "]]")}]";
                    string safeCol = $"[{fn.Key.Replace("]", "]]")}]";
                    this.Command.CommandText = $"ALTER TABLE {safeTable} ADD {safeCol} {sqlType} NULL";
                    this.Command.Parameters.Clear();
                    this.Command.ExecuteNonQuery();
                    updatedColumns++;
                }
                catch (Exception ex)
                {
                    failedColumns++;
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] SQLInteraction.UpdateTableDesign: Failed to add column: {fn.Key} - {ex.Message}");
                }
            }

            Logger.Log(1, "[UpdateTableDesign] Design update completed. Updated: " + updatedColumns + ", Failed: " + failedColumns);
        }

        private string ResolveSqlType(Field field)
        {
            string sqlType = null;
            if (this.ColumnMappings != null &&
                this.ColumnMappings.TryGetValue(field.InternalName, out var mapping) &&
                !string.IsNullOrEmpty(mapping.DataType))
            {
                sqlType = mapping.DataType;
            }
            else
            {
                sqlType = this.SQLFieldType(field);
            }

            if (sqlType != null)
            {
                sqlType = sqlType.Replace(";", "").Replace("'", "").Replace("--", "");
            }

            return sqlType;
        }

        private void TransferData(string snapDate)
        {
            Logger.Log(1, "[TransferData] Beginning data transfer for snapshot: " + snapDate);

            var fieldList = new List<Field>(this.FNDictionary.Values);
            string safeTable = $"[{this.TableName.Replace("]", "]]")}]";

            // Build DataTable schema matching the SQL table
            var dataTable = new DataTable { Locale = CultureInfo.InvariantCulture };
            dataTable.Columns.Add("Snapshot", typeof(string));
            foreach (var fn in this.FNDictionary)
                dataTable.Columns.Add(fn.Key, typeof(object));

            // Determine item source: paginated AllItems or direct ItemCollection
            var items = this.List.AllItems ?? this.List.ItemCollection?.Cast<ListItem>().ToList();

            if (items == null || items.Count == 0)
            {
                Logger.Log(1, "[TransferData] No items to transfer.");
                return;
            }

            int processedItems = 0;
            int failedItems = 0;

            foreach (ListItem listItem in items)
            {
                try
                {
                    var row = dataTable.NewRow();
                    row["Snapshot"] = snapDate;

                    int idx = 0;
                    foreach (Field field in fieldList)
                    {
                        string colName = FNDictionary.Keys.ElementAt(idx);
                        object obj = listItem[field.InternalName];
                        row[colName] = obj switch
                        {
                            null => DBNull.Value,
                            FieldLookupValue l => l.LookupId,
                            FieldUserValue u => u.LookupId,
                            FieldUrlValue url => (object)url.Url ?? DBNull.Value,
                            ContentTypeId ct => ct.StringValue,
                            DateTime dt => dt,
                            FieldLookupValue[] lookups => string.Join(";", lookups.Select(l => l.LookupId)),
                            FieldUserValue[] users => string.Join(";", users.Select(u => u.LookupId)),
                            _ => obj
                        };
                        idx++;
                    }

                    dataTable.Rows.Add(row);
                    processedItems++;
                }
                catch (Exception ex)
                {
                    failedItems++;
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] SQLInteraction.TransferData: Failed to process item {processedItems + failedItems} - {ex.Message}");
                }
            }

            // Bulk insert into SQL
            try
            {
                using var bulkCopy = new SqlBulkCopy(this.Connection)
                {
                    DestinationTableName = safeTable,
                    BatchSize = 1000,
                    BulkCopyTimeout = this.CommandTimeoutSeconds
                };

                foreach (DataColumn col in dataTable.Columns)
                    bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);

                bulkCopy.WriteToServer(dataTable);
                Logger.Log(1, $"[TransferData] Bulk insert completed: {processedItems} rows");
            }
            catch (Exception ex)
            {
                failedItems += processedItems;
                processedItems = 0;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] SQLInteraction.TransferData: Bulk insert failed - {ex.Message}");
                throw;
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

        private string GetLastSyncDate()
        {
            try
            {
                this.Command.CommandText = "SELECT LastRefreshDate FROM Metadata WHERE TableName = @TableName";
                this.Command.Parameters.Clear();
                this.Command.Parameters.AddWithValue("@TableName", this.TableName);
                object result = this.Command.ExecuteScalar();

                if (result == null || result == DBNull.Value)
                    return null;

                if (result is DateTime dt)
                    return dt.ToString(DATE_FORMAT);

                string str = result.ToString();
                return string.IsNullOrEmpty(str) ? null : str;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[GetLastSyncDate] Failed to read last sync date for table '{this.TableName}': {ex.Message}");
                return null;
            }
        }

        private void UpdateMetadata()
        {
            Logger.Log(1, $"[UpdateMetadata] Updating metadata for table: {this.TableName}");
            try
            {
                this.Command.CommandText = "DELETE FROM Metadata WHERE TableName = @TableName";
                this.Command.Parameters.Clear();
                this.Command.Parameters.AddWithValue("@TableName", this.TableName);
                this.Command.ExecuteNonQuery();
                this.Command.CommandText = "INSERT INTO Metadata (TableName, LastRefreshDate) VALUES (@TableName, @CurrentTime)";
                this.Command.Parameters.Clear();
                this.Command.Parameters.AddWithValue("@TableName", this.TableName);
                this.Command.Parameters.AddWithValue("@CurrentTime", this.CurrentTime);
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
            this.Command.CommandTimeout = this.CommandTimeoutSeconds;
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