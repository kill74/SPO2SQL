using System.Data;
using System.Globalization;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.SharePoint.Client;
using SPO2SQL.Logging;
using SPO2SQL.SharePoint;
using SPO2SQL.XmlConfig;

namespace SPO2SQL.SqlServer;

internal sealed class SQLInteraction : IDisposable
{
    private static readonly DateTime FutureSnapshot = new(2100, 1, 1, 0, 0, 0, DateTimeKind.Utc);

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

    public SqlConnection? Connection { get; private set; }
    public SqlCommand? Command { get; private set; }
    public SqlTransaction? Transaction { get; private set; }
    public SPOList List { get; set; } = null!;
    public string TableName { get; private set; } = string.Empty;
    public Dictionary<string, Field> FNDictionary { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Watermark captured before the SharePoint read. Reusing this value on the next
    /// incremental run ensures items changed while the sync was running are not lost.
    /// </summary>
    public DateTime CurrentTime { get; private set; }

    public bool DailyMode { get; set; }
    public int CommandTimeoutSeconds { get; set; } = 300;

    /// <summary>Rows sent to SQL Server per bulk-copy batch.</summary>
    public int BatchSize { get; set; } = 80;

    public CancellationToken CancellationToken { get; set; }

    /// <summary>Items successfully written during the last transfer.</summary>
    public int ProcessedItems { get; private set; }

    /// <summary>Items that failed to be written during the last transfer.</summary>
    public int FailedItems { get; private set; }

    private HashSet<string>? IgnoredColumns { get; set; }
    public Dictionary<string, ColumnMapping>? ColumnMappings { get; private set; }

    private static string? SanitizeSqlType(string? dataType) =>
        dataType != null && ValidSqlTypes.Contains(dataType.Trim()) ? dataType.Trim() : null;

    public void Dispose()
    {
        if (!_transactionCommitted)
        {
            SafeRollback();
        }

        Transaction?.Dispose();
        Command?.Dispose();
        Connection?.Dispose();
    }

    public async Task BuildAsync()
    {
        ArgumentNullException.ThrowIfNull(List);

        Logger.Log(1, "[Build] Starting SQL build process for list: " + List.Name);

        IgnoredColumns = ConfigurationReader.GetIgnoredColumns();
        TableName = ToPascalCase(List.Name, false);

        Connection = new SqlConnection(ConfigurationReader.GetSqlConnectionString());
        Logger.Log(1, "[Build] Establishing SQL connection...");
        await Connection.OpenAsync(CancellationToken).ConfigureAwait(false);

        InitializeCommand();
        await EnsureMetadataTableAsync().ConfigureAwait(false);

        CurrentTime = DateTime.UtcNow;

        bool tableExists = await TableExistsAsync(TableName).ConfigureAwait(false);

        if (DailyMode && tableExists)
        {
            DateTime? lastSync = await GetLastSyncDateAsync().ConfigureAwait(false);
            if (lastSync.HasValue)
            {
                Logger.Log(1, "[Build] Applying incremental filter: Modified >= " + lastSync.Value.ToString("O", CultureInfo.InvariantCulture));
                List.CAMLQuery = CamlQueryBuilder.BuildDateRangeQuery("Modified", lastSync.Value, CurrentTime);
            }
        }

        Logger.Log(1, "[Build] Initializing SharePoint list structure...");
        await List.BuildAsync(CancellationToken).ConfigureAwait(false);

        FNDictionary = new Dictionary<string, Field>(StringComparer.OrdinalIgnoreCase);
        BuildDictionary();

        if (!tableExists)
        {
            Logger.Log(1, "[Build] Creating new table: " + TableName);
            await CreateTableAsync().ConfigureAwait(false);
        }
        else
        {
            Logger.Log(1, "[Build] Updating existing table: " + TableName);
            await UpdateTableDesignAsync().ConfigureAwait(false);
        }
    }

    public async Task DailyUpdateAsync()
    {
        try
        {
            Logger.Log(2, "[DailyUpdate] Starting daily update for table " + TableName);

            BeginTransaction();

            Command!.CommandText = $"DELETE FROM [{TableName.Replace("]", "]]")}] WHERE Snapshot = @FutureDate";
            Command.Parameters.Clear();
            Command.Parameters.AddWithValue("@FutureDate", FutureSnapshot);
            await Command.ExecuteNonQueryAsync(CancellationToken).ConfigureAwait(false);

            await TransferDataAsync(FutureSnapshot).ConfigureAwait(false);
            await UpdateMetadataAsync().ConfigureAwait(false);

            await Transaction!.CommitAsync(CancellationToken).ConfigureAwait(false);
            _transactionCommitted = true;
            Logger.Log(2, "[DailyUpdate] Transaction committed successfully");
        }
        catch (Exception ex)
        {
            Logger.LogError("[DailyUpdate] Critical failure during daily update", ex);
            SafeRollback();
            throw;
        }
    }

    public async Task CurrentTimeUpdateAsync()
    {
        try
        {
            Logger.Log(2, "[CurrentTimeUpdate] Starting current-time update for " + TableName);

            BeginTransaction();

            await TransferDataAsync(CurrentTime).ConfigureAwait(false);
            await UpdateMetadataAsync().ConfigureAwait(false);

            await Transaction!.CommitAsync(CancellationToken).ConfigureAwait(false);
            _transactionCommitted = true;
            Logger.Log(2, "[CurrentTimeUpdate] Transaction committed successfully");
        }
        catch (Exception ex)
        {
            Logger.LogError("[CurrentTimeUpdate] Critical failure during current-time update", ex);
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
            if (field.TypeAsString == "Computed")
            {
                continue;
            }

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
                            continue;
                        }

                        string destinationName = mapping.Destination;
                        FNDictionary.Add(GetKeyName(destinationName, 1), field);
                        processedFields++;
                    }
                    else
                    {
                        skippedFields++;
                    }
                }
                else if (IgnoredColumns != null && IgnoredColumns.Contains(columnName))
                {
                    ignoredFields++;
                }
                else
                {
                    FNDictionary.Add(GetKeyName(columnName, 1), field);
                    processedFields++;
                }
            }
            catch (Exception ex)
            {
                skippedFields++;
                Logger.LogError($"Failed to process field '{field.Title}'", ex);
            }
        }

        Logger.Log(1, $"[BuildDictionary] Dictionary built. Processed: {processedFields}, Skipped: {skippedFields}, Ignored: {ignoredFields}");
    }

    private async Task<bool> TableExistsAsync(string tableName)
    {
        Command!.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @TableName";
        Command.Parameters.Clear();
        Command.Parameters.AddWithValue("@TableName", tableName);
        bool exists = (int)(await Command.ExecuteScalarAsync(CancellationToken).ConfigureAwait(false))! != 0;
        Logger.Log(1, $"[TableExists] Table '{tableName}' exists: {exists}");
        return exists;
    }

    private async Task CreateTableAsync()
    {
        var columns = new List<string> { "[Snapshot] datetime2 NULL" };

        foreach (var fn in FNDictionary)
        {
            string? sqlType = ResolveSqlType(fn.Value);
            if (sqlType != null)
            {
                columns.Add($"[{fn.Key.Replace("]", "]]")}] {sqlType} NULL");
            }
        }

        Command!.CommandText =
            $"CREATE TABLE [{TableName.Replace("]", "]]")}] ({string.Join(", ", columns)})";

        await Command.ExecuteNonQueryAsync(CancellationToken).ConfigureAwait(false);
        Logger.Log(1, "[CreateTable] Successfully created table: " + TableName);
    }

    private async Task UpdateTableDesignAsync()
    {
        int updatedColumns = 0;
        int failedColumns = 0;

        foreach (var fn in FNDictionary)
        {
            string? sqlType = ResolveSqlType(fn.Value);
            if (sqlType == null)
            {
                failedColumns++;
                continue;
            }

            try
            {
                string colName = fn.Key;

                Command!.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @TableName AND COLUMN_NAME = @ColName";
                Command.Parameters.Clear();
                Command.Parameters.AddWithValue("@TableName", TableName);
                Command.Parameters.AddWithValue("@ColName", colName);
                if ((int)(await Command.ExecuteScalarAsync(CancellationToken).ConfigureAwait(false))! == 0)
                {
                    string safeTable = $"[{TableName.Replace("]", "]]")}]";
                    string safeCol = $"[{colName.Replace("]", "]]")}]";
                    Command.CommandText = $"ALTER TABLE {safeTable} ADD {safeCol} {sqlType} NULL";
                    Command.Parameters.Clear();
                    await Command.ExecuteNonQueryAsync(CancellationToken).ConfigureAwait(false);
                    updatedColumns++;
                }
            }
            catch (Exception ex)
            {
                failedColumns++;
                Logger.LogError($"Failed to update column '{fn.Key}'", ex);
            }
        }

        Logger.Log(1, $"[UpdateTableDesign] Design update completed. Updated: {updatedColumns}, Failed: {failedColumns}");
    }

    private string? ResolveSqlType(Field field)
    {
        if (ColumnMappings != null &&
            ColumnMappings.TryGetValue(field.InternalName, out var mapping) &&
            !string.IsNullOrEmpty(mapping.DataType))
        {
            return SanitizeSqlType(mapping.DataType) ?? SQLFieldType(field);
        }

        return SQLFieldType(field);
    }

    /// <summary>
    /// Materializes the SharePoint items once and writes them with a single bulk
    /// operation instead of one command per row. All conversion work happens before
    /// the SQL write, so the transaction stays open for the shortest possible time.
    /// </summary>
    private async Task TransferDataAsync(DateTime snapDate)
    {
        Logger.Log(1, "[TransferData] Building bulk-copy batch for snapshot: " + snapDate);

        using DataTable batch = BuildInsertBatch(snapDate);
        if (batch.Rows.Count == 0)
        {
            ProcessedItems = 0;
            FailedItems = 0;
            Logger.Log(1, "[TransferData] No items to transfer.");
            return;
        }

        string safeTable = $"[{TableName.Replace("]", "]]")}]";

        using var bulkCopy = new SqlBulkCopy(
            Connection!,
            SqlBulkCopyOptions.TableLock |
            SqlBulkCopyOptions.CheckConstraints |
            SqlBulkCopyOptions.FireTriggers,
            Transaction)
        {
            DestinationTableName = safeTable,
            BatchSize = BatchSize,
            BulkCopyTimeout = CommandTimeoutSeconds
        };

        foreach (DataColumn column in batch.Columns)
        {
            bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        }

        await bulkCopy.WriteToServerAsync(batch, CancellationToken).ConfigureAwait(false);

        ProcessedItems = batch.Rows.Count;
        FailedItems = 0;

        Logger.Log(1, $"[TransferData] Transfer completed. Processed: {ProcessedItems}, Failed: 0");
    }

    private DataTable BuildInsertBatch(DateTime snapDate)
    {
        var batch = new DataTable();
        batch.Columns.Add("Snapshot", typeof(DateTime));
        foreach (var mapping in FNDictionary)
        {
            batch.Columns.Add(mapping.Key, typeof(object));
        }

        foreach (ListItem item in List.ItemCollection)
        {
            CancellationToken.ThrowIfCancellationRequested();

            DataRow row = batch.NewRow();
            row["Snapshot"] = snapDate;

            foreach (var mapping in FNDictionary)
            {
                object rawValue;
                try
                {
                    rawValue = item[mapping.Value.InternalName];
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to read field '{mapping.Value.InternalName}' for SharePoint item.", ex);
                }

                row[mapping.Key] = ConvertFieldValue(rawValue) ?? DBNull.Value;
            }

            batch.Rows.Add(row);
        }

        return batch;
    }

    private static object ConvertFieldValue(object obj) => obj switch
    {
        FieldUserValue user => user.LookupId,
        FieldLookupValue lookup => lookup.LookupId,
        FieldUrlValue url => (object?)url.Url ?? DBNull.Value,
        ContentTypeId ctId => ctId.StringValue,
        DateTime dt => dt,
        FieldUserValue[] users => users.Length > 0
            ? string.Join(";", users.Select(u => u.LookupId))
            : DBNull.Value,
        FieldLookupValue[] lookups => lookups.Length > 0
            ? string.Join(";", lookups.Select(l => l.LookupId))
            : DBNull.Value,
        null => DBNull.Value,
        _ => obj
    };

    private async Task EnsureMetadataTableAsync()
    {
        Command!.CommandText = @"
IF OBJECT_ID(N'[Metadata]', N'U') IS NULL
BEGIN
    CREATE TABLE [Metadata]
    (
        [TableName] nvarchar(255) NOT NULL CONSTRAINT [PK_Metadata] PRIMARY KEY,
        [LastRefreshDate] datetime2 NULL
    );
END";
        Command.Parameters.Clear();
        await Command.ExecuteNonQueryAsync(CancellationToken).ConfigureAwait(false);
    }

    private static string? SQLFieldType(Field field) => field.TypeAsString switch
    {
        "Attachments" or "Boolean" => "[bit]",
        "Calculated" => "[sql_variant]",
        "Choice" or "File" or "LookupMulti" or "Note" or "Text" or "URL" or
        "UserMulti" or "ContentTypeId" => "[nvarchar](MAX)",
        "Counter" or "Integer" or "ModStat" or "User" => "[int]",
        "Currency" or "Number" => "[float]",
        "DateTime" => "[datetime]",
        "Guid" => "[uniqueidentifier]",
        "Lookup" => field.FromBaseType ? "[nvarchar](MAX)" : "[int]",
        _ => LogUnknownFieldType(field)
    };

    private static string? LogUnknownFieldType(Field field)
    {
        Logger.Log(1, $"[SQLFieldType] Unknown field type encountered - Field: {field.Title}, Type: {field.TypeAsString}");
        return null;
    }

    private async Task<DateTime?> GetLastSyncDateAsync()
    {
        Command!.CommandText = "SELECT LastRefreshDate FROM Metadata WHERE TableName = @TableName";
        Command.Parameters.Clear();
        Command.Parameters.AddWithValue("@TableName", TableName);
        object? result = await Command.ExecuteScalarAsync(CancellationToken).ConfigureAwait(false);

        if (result == null || result == DBNull.Value)
        {
            return null;
        }

        if (result is DateTime dt)
        {
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }

        return DateTime.TryParse(
            result.ToString(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out DateTime parsed)
            ? parsed
            : null;
    }

    private async Task UpdateMetadataAsync()
    {
        Logger.Log(1, $"[UpdateMetadata] Updating metadata for table: {TableName}");

        Command!.CommandText = "DELETE FROM Metadata WHERE TableName = @TableName";
        Command.Parameters.Clear();
        Command.Parameters.AddWithValue("@TableName", TableName);
        await Command.ExecuteNonQueryAsync(CancellationToken).ConfigureAwait(false);

        Command.CommandText = "INSERT INTO Metadata (TableName, LastRefreshDate) VALUES (@TableName, @CurrentTime)";
        Command.Parameters.Clear();
        Command.Parameters.AddWithValue("@TableName", TableName);
        Command.Parameters.AddWithValue("@CurrentTime", CurrentTime);
        await Command.ExecuteNonQueryAsync(CancellationToken).ConfigureAwait(false);

        Logger.Log(1, "[UpdateMetadata] Metadata updated successfully");
    }

    private string GetKeyName(string key, int i = 1)
    {
        string testKey = i == 1 ? key : $"{key}{i}";
        return FNDictionary.ContainsKey(testKey) ? GetKeyName(key, i + 1) : testKey;
    }

    private void InitializeCommand()
    {
        Command = Connection!.CreateCommand();
        Command.CommandTimeout = CommandTimeoutSeconds;
    }

    /// <summary>
    /// Starts the write transaction only after all SharePoint data has been read and
    /// transformed, so no database locks are held during network calls.
    /// </summary>
    private void BeginTransaction()
    {
        Transaction = Connection!.BeginTransaction($"{TableName} TXN");
        Command!.Transaction = Transaction;
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
            Logger.LogError("[SafeRollback] Failed to rollback transaction", ex);
        }
    }

    private static string ToPascalCase(string text, bool internalName)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        if (internalName && text.StartsWith('_'))
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
}
