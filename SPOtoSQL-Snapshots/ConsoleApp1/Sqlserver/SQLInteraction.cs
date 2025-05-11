using Bring.Sharepoint;
using Bring.XmlConfig;
using Microsoft.SharePoint.Client;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.Text;

namespace Bring.Sqlserver
{
    /// <summary>
    /// Encapsulates interactions between a SharePoint list and a SQL Server database,
    /// including schema creation, updates, and data transfer.
    /// </summary>
    internal class SQLInteraction
    {
        // SQL connection, command and transaction objects
        public SqlConnection Connection { get; set; }
        public SqlCommand Command { get; set; }
        public SqlTransaction Transaction { get; set; }

        // SharePoint list being processed
        public SPOList List { get; set; }

        // Name of the SQL table corresponding to the list
        public string TableName { get; set; }

        // Mapping of SQL column names to SharePoint Field definitions
        public Dictionary<string, Field> FNDictionary { get; set; }

        // Timestamp for the current operation
        public string CurrentTime { get; set; }

        /// <summary>
        /// Initializes the SQL table for the specified SharePoint list,
        /// creating or updating schema as necessary.
        /// </summary>
        public void Build()
        {
            Console.WriteLine("Starting SQL build for list: " + this.List.Name);

            // Convert list name to PascalCase to use as the SQL table name
            this.TableName = this.ToPascalCase(this.List.Name, false);

            // Open database connection and begin transaction
            this.Connection = new SqlConnection(ConfigurationReader.GetSqlConnectionString());
            Console.WriteLine("Opening SQL connection...");
            this.Connection.Open();

            this.Command = this.Connection.CreateCommand();
            this.Transaction = this.Connection.BeginTransaction(this.TableName + " TXN.");
            this.Command.Connection = this.Connection;
            this.Command.Transaction = this.Transaction;

            // Capture current timestamp
            this.CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

            // Load SharePoint list metadata
            Console.WriteLine("Building SPO list...");
            this.List.Build();

            // Build dictionary of columns based on fields
            this.FNDictionary = new Dictionary<string, Field>(StringComparer.OrdinalIgnoreCase);
            Console.WriteLine("Building dictionary of fields...");
            this.BuildDictionary();

            // Create or update table schema
            if (!this.TableExists(this.TableName))
            {
                Console.WriteLine("Table doesn't exist. Creating table: " + this.TableName);
                this.CreateTable();
            }
            else
            {
                Console.WriteLine("Table exists. Updating table design...");
                this.UpdateTableDesign();
            }
        }

        /// <summary>
        /// Performs a daily data refresh by deleting placeholder rows,
        /// transferring new data, and updating metadata.
        /// </summary>
        public void DailyUpdate()
        {
            try
            {
                Console.WriteLine("Performing daily update...");

                // Remove previous snapshot marker rows
                this.Command.CommandText = "DELETE FROM [" + this.TableName + "] WHERE Snapshot = '2100-01-01 00:00:00.000'";
                this.Command.ExecuteNonQuery();

                // Transfer new rows with far-future snapshot marker
                this.TransferData("2100-01-01 00:00:00.000");
                this.UpdateMetadata();
                this.Transaction.Commit();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Daily update failed: " + ex.Message);
                this.Transaction.Rollback();
            }

            Console.WriteLine("Daily Update done for: " + this.TableName);
        }

        /// <summary>
        /// Appends only the most recent data snapshot and commits it immediately.
        /// </summary>
        public void CurrentTimeUpdate()
        {
            try
            {
                Console.WriteLine("Performing current-time update...");
                this.TransferData(this.CurrentTime);
                this.UpdateMetadata();
                this.Transaction.Commit();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Current time update failed: " + ex.Message);
                this.Transaction.Rollback();
            }

            Console.WriteLine(this.CurrentTime + " Update done for: " + this.TableName);
        }

        /// <summary>
        /// Inserts all items from the SharePoint list into the SQL table
        /// using the provided snapshot date.
        /// </summary>
        private void TransferData(string snapDate)
        {
            Console.WriteLine("Transferring data for snapshot: " + snapDate);
            StringBuilder stringBuilder = new StringBuilder();
            string sqlColNames = this.GetSQLColNames();

            foreach (ListItem listItem in this.List.ItemCollection)
            {
                // Build INSERT statement
                stringBuilder.Clear();
                stringBuilder.AppendLine("INSERT INTO [" + this.TableName + "] " + sqlColNames);
                stringBuilder.Append("VALUES ('" + snapDate + "', ");

                foreach (Field field in this.FNDictionary.Values)
                {
                    object obj = listItem[field.InternalName];
                    // Handle different field types appropriately
                    if (obj != null)
                    {
                        if (obj is FieldLookupValue lookup)
                            stringBuilder.Append("'" + lookup.LookupId + "', ");
                        else if (obj is FieldUserValue user)
                            stringBuilder.Append(user.LookupId.ToString() + ", ");
                        else if (obj is FieldUrlValue url)
                            stringBuilder.Append("'" + url.Url + "', ");
                        else if (obj is ContentTypeId)
                            stringBuilder.Append("'" + obj.ToString() + "', ");
                        else if (obj is DateTime dt)
                            stringBuilder.AppendFormat("'{0:yyyy-MM-dd HH:mm:ss.fff}', ", dt);
                        else if (obj is FieldLookupValue[] lookups)
                        {
                            stringBuilder.Append("'");
                            foreach (var l in lookups) stringBuilder.Append(l.LookupId + ";");
                            stringBuilder.Append("', ");
                        }
                        else if (obj is FieldUserValue[] users)
                        {
                            stringBuilder.Append("'");
                            foreach (var u in users) stringBuilder.Append(u.LookupId + ";");
                            stringBuilder.Append("', ");
                        }
                        else
                        {
                            // Escape single quotes for text fields
                            if (obj is string s) obj = s.Replace("'", "''");
                            stringBuilder.Append("'" + obj + "', ");
                        }
                    }
                    else
                        stringBuilder.Append("NULL, ");
                }

                // Remove trailing comma and execute
                stringBuilder.Remove(stringBuilder.Length - 2, 2);
                stringBuilder.Append(")");
                this.Command.CommandText = stringBuilder.ToString();

                try
                {
                    this.Command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Couldn't insert values: " + ex.Message);
                    Console.WriteLine("INSERT STATEMENT: " + stringBuilder.ToString());
                }
            }
        }

        /// <summary>
        /// Checks if the SQL table already exists in the database.
        /// </summary>
        private bool TableExists(string listName)
        {
            this.Command.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '" + listName + "'";
            bool exists = (int)this.Command.ExecuteScalar() != 0;
            Console.WriteLine("Table " + listName + " exists: " + exists);
            return exists;
        }

        /// <summary>
        /// Creates a new SQL table based on the fields dictionary.
        /// </summary>
        private void CreateTable()
        {
            Console.WriteLine("Creating new table: " + this.TableName);
            StringBuilder stringBuilder = new StringBuilder("CREATE TABLE [");
            stringBuilder.Append(this.TableName);
            stringBuilder.AppendLine("] (");
            stringBuilder.AppendLine("[Snapshot] datetime NULL,");

            foreach (var fn in this.FNDictionary)
            {
                string sqlType = this.SQLFieldType(fn.Value);
                if (sqlType != null)
                    stringBuilder.AppendLine("[" + fn.Key + "] " + sqlType + " NULL,");
            }

            // Remove trailing comma
            stringBuilder.Remove(stringBuilder.Length - 3, 3);
            stringBuilder.Append(")");

            this.Command.CommandText = stringBuilder.ToString();
            try
            {
                this.Command.ExecuteNonQuery();
                Console.WriteLine("Created table: " + this.TableName);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Could not create table: " + ex.Message);
                Console.WriteLine("CREATE TABLE STATEMENT: " + stringBuilder.ToString());
            }
        }

        /// <summary>
        /// Populates the field dictionary with SharePoint fields,
        /// excluding computed fields, ensuring unique SQL column names.
        /// </summary>
        private void BuildDictionary()
        {
            Console.WriteLine("Building FNDictionary from fields...");
            foreach (Field field in this.List.Fields)
            {
                if (field.TypeAsString != "Computed")
                    this.FNDictionary.Add(this.GetKeyName(this.GetActualColName(field), 1), field);
            }
        }

        /// <summary>
        /// Generates a unique key name for a field by appending an index if needed.
        /// </summary>
        private string GetKeyName(string key, int i = 1)
        {
            string testKey = i == 1 ? key : key + i;
            return this.FNDictionary.ContainsKey(testKey)
                ? this.GetKeyName(key, i + 1)
                : testKey;
        }

        /// <summary>
        /// Determines the actual column name, handling duplicates by
        /// falling back to the field's internal name in PascalCase.
        /// </summary>
        private string GetActualColName(Field pField)
        {
            string name = this.ColNameConvetions(pField);
            int count = 0;

            foreach (Field field in this.List.Fields)
            {
                if (field.TypeAsString != "Computed" &&
                    name.Equals(this.ColNameConvetions(field), StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count > 1
                ? this.ToPascalCase(pField.InternalName, true)
                : name;
        }

        /// <summary>
        /// Applies naming conventions based on field title and type.
        /// </summary>
        private string ColNameConvetions(Field pField)
        {
            var sb = new StringBuilder(this.ToPascalCase(pField.Title, false));
            string type = pField.TypeAsString;

            if (type == "Choice")
                sb.Append("Value");
            else if (type == "User" || (type == "Lookup" && !pField.FromBaseType))
                sb.Append("Id");

            return sb.ToString();
        }

        /// <summary>
        /// Maps SharePoint field types to appropriate SQL data types.
        /// </summary>
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
                    Console.WriteLine(field.Title + " has unknown type: " + field.TypeAsString);
                    return null;
            }
        }

        /// <summary>
        /// Alters the existing table to match the current field dictionary,
        /// adding or modifying columns as needed.
        /// </summary>
        private void UpdateTableDesign()
        {
            Console.WriteLine("Updating table design...");
            foreach (var fn in this.FNDictionary)
            {
                string sqlType = this.SQLFieldType(fn.Value);
                string baseType = sqlType.Substring(sqlType.IndexOf('[') + 1,
                                                    sqlType.LastIndexOf(']') - sqlType.IndexOf('[') - 1);
                string colName = fn.Key;

                // Check if column exists
                this.Command.CommandText = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{this.TableName}' AND COLUMN_NAME = '{colName}'";
                if ((int)this.Command.ExecuteScalar() == 0)
                {
                    Console.WriteLine($"Adding new column: {colName}");
                    this.Command.CommandText = $"ALTER TABLE [{this.TableName}] ADD [{colName}] {sqlType}";
                    this.Command.ExecuteNonQuery();
                }
                else
                {
                    // Check if type matches; alter if necessary
                    this.Command.CommandText = $"SELECT [DATA_TYPE] FROM LAKEDB.INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{this.TableName}' AND COLUMN_NAME = '{colName}'";
                    if ((string)this.Command.ExecuteScalar() != baseType)
                    {
                        Console.WriteLine($"Altering column: {colName}");
                        this.Command.CommandText = $"ALTER TABLE [{this.TableName}] ALTER COLUMN [{colName}] {sqlType}";
                        this.Command.ExecuteNonQuery();
                    }
                }
            }
        }

        /// <summary>
        /// Builds the column list segment for INSERT statements.
        /// </summary>
        private string GetSQLColNames()
        {
            var sb = new StringBuilder();
            sb.Append("([Snapshot], ");
            foreach (var fn in this.FNDictionary)
                sb.Append("[" + fn.Key + "], ");
            sb.Remove(sb.Length - 2, 2);
            sb.Append(")");
            return sb.ToString();
        }

        /// <summary>
        /// Updates a metadata table to record the last refresh time for this list.
        /// </summary>
        private void UpdateMetadata()
        {
            Console.WriteLine("Updating metadata...");
            this.Command.CommandText = $"DELETE FROM Metadata WHERE TableName = '{this.TableName}'";
            this.Command.ExecuteNonQuery();
            this.Command.CommandText = $"INSERT INTO Metadata (TableName, LastRefreshDate) Values ('{this.TableName}', '{this.CurrentTime}')";
            this.Command.ExecuteNonQuery();
        }

        /// <summary>
        /// Converts arbitrary text into PascalCase, preserving alphanumeric characters.
        /// Appends 'IN' if internal field name starts with underscore.
        /// </summary>
        private string ToPascalCase(string text, bool internalName)
        {
            if (internalName && text.StartsWith("_"))
                text += "IN";

            // Replace non-alphanumeric with spaces, then TitleCase
            var sanitized = new StringBuilder();
            foreach (char c in text)
                sanitized.Append(char.IsLetterOrDigit(c) ? c : ' ');

            return CultureInfo.InvariantCulture.TextInfo
                .ToTitleCase(sanitized.ToString())
                .Replace(" ", string.Empty)
                .Replace("X0020", string.Empty)
                .Replace("X003a", string.Empty);
        }
    }
}
