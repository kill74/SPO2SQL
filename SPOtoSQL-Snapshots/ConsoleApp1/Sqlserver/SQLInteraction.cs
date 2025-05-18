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
        public SqlConnection Connection { get; set; }
        public SqlCommand Command { get; set; }
        public SqlTransaction Transaction { get; set; }
        public SPOList List { get; set; }
        public string TableName { get; set; }
        public Dictionary<string, Field> FNDictionary { get; set; }
        public string CurrentTime { get; set; }

        /// <summary>
        /// Initializes the SQL table for the specified SharePoint list,
        /// creating or updating schema as necessary.
        /// </summary>
        public void Build()
        {
            Console.WriteLine("SQLInteraction.Build: Starting SQL build for list: " + (this.List?.Name ?? "null"));

            try
            {
                this.TableName = this.ToPascalCase(this.List.Name, false);

                try
                {
                    this.Connection = new SqlConnection(ConfigurationReader.GetSqlConnectionString());
                    Console.WriteLine("SQLInteraction.Build: Opening SQL connection...");
                    this.Connection.Open();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("SQLInteraction.Build: ERROR - Failed to open SQL connection.");
                    Console.WriteLine("Exception: " + ex.Message);
                    Console.WriteLine("Stack Trace: " + ex.StackTrace);
                    throw;
                }

                this.Command = this.Connection.CreateCommand();
                this.Transaction = this.Connection.BeginTransaction(this.TableName + " TXN.");
                this.Command.Connection = this.Connection;
                this.Command.Transaction = this.Transaction;

                this.CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                try
                {
                    Console.WriteLine("SQLInteraction.Build: Building SPO list...");
                    this.List.Build();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("SQLInteraction.Build: ERROR - Failed to build SharePoint list.");
                    Console.WriteLine("Exception: " + ex.Message);
                    Console.WriteLine("Stack Trace: " + ex.StackTrace);
                    throw;
                }

                this.FNDictionary = new Dictionary<string, Field>(StringComparer.OrdinalIgnoreCase);
                Console.WriteLine("SQLInteraction.Build: Building dictionary of fields...");
                try
                {
                    this.BuildDictionary();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("SQLInteraction.Build: ERROR - Failed to build field dictionary.");
                    Console.WriteLine("Exception: " + ex.Message);
                    Console.WriteLine("Stack Trace: " + ex.StackTrace);
                    throw;
                }

                try
                {
                    if (!this.TableExists(this.TableName))
                    {
                        Console.WriteLine("SQLInteraction.Build: Table doesn't exist. Creating table: " + this.TableName);
                        this.CreateTable();
                    }
                    else
                    {
                        Console.WriteLine("SQLInteraction.Build: Table exists. Updating table design...");
                        this.UpdateTableDesign();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("SQLInteraction.Build: ERROR - Failed during table creation or update.");
                    Console.WriteLine("Exception: " + ex.Message);
                    Console.WriteLine("Stack Trace: " + ex.StackTrace);
                    throw;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("SQLInteraction.Build: FATAL ERROR - Build process failed.");
                Console.WriteLine("Exception: " + ex.Message);
                Console.WriteLine("Stack Trace: " + ex.StackTrace);
                throw;
            }
        }

        public void DailyUpdate()
        {
            try
            {
                Console.WriteLine("SQLInteraction.DailyUpdate: Performing daily update...");

                try
                {
                    this.Command.CommandText = "DELETE FROM [" + this.TableName + "] WHERE Snapshot = '2100-01-01 00:00:00.000'";
                    this.Command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("SQLInteraction.DailyUpdate: ERROR - Failed to delete previous snapshot marker rows.");
                    Console.WriteLine("Exception: " + ex.Message);
                }

                try
                {
                    this.TransferData("2100-01-01 00:00:00.000");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("SQLInteraction.DailyUpdate: ERROR - Failed to transfer data.");
                    Console.WriteLine("Exception: " + ex.Message);
                }

                try
                {
                    this.UpdateMetadata();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("SQLInteraction.DailyUpdate: ERROR - Failed to update metadata.");
                    Console.WriteLine("Exception: " + ex.Message);
                }

                try
                {
                    this.Transaction.Commit();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("SQLInteraction.DailyUpdate: ERROR - Failed to commit transaction.");
                    Console.WriteLine("Exception: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("SQLInteraction.DailyUpdate: FATAL ERROR - Daily update failed: " + ex.Message);
                Console.WriteLine("Stack Trace: " + ex.StackTrace);
                try { this.Transaction?.Rollback(); } catch { }
            }

            Console.WriteLine("SQLInteraction.DailyUpdate: Daily Update done for: " + this.TableName);
        }

        public void CurrentTimeUpdate()
        {
            try
            {
                Console.WriteLine("SQLInteraction.CurrentTimeUpdate: Performing current-time update...");
                try
                {
                    this.TransferData(this.CurrentTime);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("SQLInteraction.CurrentTimeUpdate: ERROR - Failed to transfer data.");
                    Console.WriteLine("Exception: " + ex.Message);
                }

                try
                {
                    this.UpdateMetadata();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("SQLInteraction.CurrentTimeUpdate: ERROR - Failed to update metadata.");
                    Console.WriteLine("Exception: " + ex.Message);
                }

                try
                {
                    this.Transaction.Commit();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("SQLInteraction.CurrentTimeUpdate: ERROR - Failed to commit transaction.");
                    Console.WriteLine("Exception: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("SQLInteraction.CurrentTimeUpdate: FATAL ERROR - Current time update failed: " + ex.Message);
                Console.WriteLine("Stack Trace: " + ex.StackTrace);
                try { this.Transaction?.Rollback(); } catch { }
            }

            Console.WriteLine(this.CurrentTime + " Update done for: " + this.TableName);
        }

        private void TransferData(string snapDate)
        {
            Console.WriteLine("SQLInteraction.TransferData: Transferring data for snapshot: " + snapDate);
            StringBuilder stringBuilder = new StringBuilder();
            string sqlColNames = this.GetSQLColNames();

            foreach (ListItem listItem in this.List.ItemCollection)
            {
                stringBuilder.Clear();
                stringBuilder.AppendLine("INSERT INTO [" + this.TableName + "] " + sqlColNames);
                stringBuilder.Append("VALUES ('" + snapDate + "', ");

                foreach (Field field in this.FNDictionary.Values)
                {
                    object obj = listItem[field.InternalName];
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
                            if (obj is string s) obj = s.Replace("'", "''");
                            stringBuilder.Append("'" + obj + "', ");
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
                }
                catch (Exception ex)
                {
                    Console.WriteLine("SQLInteraction.TransferData: ERROR - Couldn't insert values: " + ex.Message);
                    Console.WriteLine("INSERT STATEMENT: " + stringBuilder.ToString());
                }
            }
        }

        private bool TableExists(string listName)
        {
            try
            {
                this.Command.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '" + listName + "'";
                bool exists = (int)this.Command.ExecuteScalar() != 0;
                Console.WriteLine("SQLInteraction.TableExists: Table " + listName + " exists: " + exists);
                return exists;
            }
            catch (Exception ex)
            {
                Console.WriteLine("SQLInteraction.TableExists: ERROR - Failed to check if table exists.");
                Console.WriteLine("Exception: " + ex.Message);
                throw;
            }
        }

        private void CreateTable()
        {
            Console.WriteLine("SQLInteraction.CreateTable: Creating new table: " + this.TableName);
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

            stringBuilder.Remove(stringBuilder.Length - 3, 3);
            stringBuilder.Append(")");

            this.Command.CommandText = stringBuilder.ToString();
            try
            {
                this.Command.ExecuteNonQuery();
                Console.WriteLine("SQLInteraction.CreateTable: Created table: " + this.TableName);
            }
            catch (Exception ex)
            {
                Console.WriteLine("SQLInteraction.CreateTable: ERROR - Could not create table: " + ex.Message);
                Console.WriteLine("CREATE TABLE STATEMENT: " + stringBuilder.ToString());
            }
        }

        private void BuildDictionary()
        {
            Console.WriteLine("SQLInteraction.BuildDictionary: Building FNDictionary from fields...");
            foreach (Field field in this.List.Fields)
            {
                if (field.TypeAsString != "Computed")
                {
                    try
                    {
                        this.FNDictionary.Add(this.GetKeyName(this.GetActualColName(field), 1), field);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("SQLInteraction.BuildDictionary: ERROR - Failed to add field to dictionary: " + field.Title);
                        Console.WriteLine("Exception: " + ex.Message);
                    }
                }
            }
        }

        private string GetKeyName(string key, int i = 1)
        {
            string testKey = i == 1 ? key : key + i;
            return this.FNDictionary.ContainsKey(testKey)
                ? this.GetKeyName(key, i + 1)
                : testKey;
        }

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
                    Console.WriteLine("SQLInteraction.SQLFieldType: " + field.Title + " has unknown type: " + field.TypeAsString);
                    return null;
            }
        }

        private void UpdateTableDesign()
        {
            Console.WriteLine("SQLInteraction.UpdateTableDesign: Updating table design...");
            foreach (var fn in this.FNDictionary)
            {
                string sqlType = this.SQLFieldType(fn.Value);
                string baseType = sqlType.Substring(sqlType.IndexOf('[') + 1,
                                                    sqlType.LastIndexOf(']') - sqlType.IndexOf('[') - 1);
                string colName = fn.Key;

                try
                {
                    this.Command.CommandText = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{this.TableName}' AND COLUMN_NAME = '{colName}'";
                    if ((int)this.Command.ExecuteScalar() == 0)
                    {
                        Console.WriteLine($"SQLInteraction.UpdateTableDesign: Adding new column: {colName}");
                        this.Command.CommandText = $"ALTER TABLE [{this.TableName}] ADD [{colName}] {sqlType}";
                        this.Command.ExecuteNonQuery();
                    }
                    else
                    {
                        this.Command.CommandText = $"SELECT [DATA_TYPE] FROM LAKEDB.INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{this.TableName}' AND COLUMN_NAME = '{colName}'";
                        if ((string)this.Command.ExecuteScalar() != baseType)
                        {
                            Console.WriteLine($"SQLInteraction.UpdateTableDesign: Altering column: {colName}");
                            this.Command.CommandText = $"ALTER TABLE [{this.TableName}] ALTER COLUMN [{colName}] {sqlType}";
                            this.Command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SQLInteraction.UpdateTableDesign: ERROR - Failed to update column '{colName}': {ex.Message}");
                }
            }
        }

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

        private void UpdateMetadata()
        {
            Console.WriteLine("SQLInteraction.UpdateMetadata: Updating metadata...");
            try
            {
                this.Command.CommandText = $"DELETE FROM Metadata WHERE TableName = '{this.TableName}'";
                this.Command.ExecuteNonQuery();
                this.Command.CommandText = $"INSERT INTO Metadata (TableName, LastRefreshDate) Values ('{this.TableName}', '{this.CurrentTime}')";
                this.Command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine("SQLInteraction.UpdateMetadata: ERROR - Failed to update metadata: " + ex.Message);
            }
        }

        private string ToPascalCase(string text, bool internalName)
        {
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
    }
}
