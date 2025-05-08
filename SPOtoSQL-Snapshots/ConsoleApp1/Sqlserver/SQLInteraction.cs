// Decompiled with JetBrains decompiler
// Type: Bring.Sqlserver.SQLInteraction
// Assembly: ConsoleApp1, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 2529ACA9-9F81-4C49-8E47-E8B02D261367
// Assembly location: C:\Users\KEVIN\Desktop\Visual Studio\SPtoSP\ConsoleApp1.exe

using Bring.Sharepoint;
using Microsoft.SharePoint.Client;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.Text;
using System.Xml;

namespace Bring.Sqlserver
{
  internal class SQLInteraction
  {
    public SqlConnection Connection { get; set; }

    public SqlCommand Command { get; set; }

    public SqlTransaction Transaction { get; set; }

    public SPOList List { get; set; }

    public string TableName { get; set; }

    public Dictionary<string, Field> FNDictionary { get; set; }

    public string CurrentTime { get; set; }

    public void Build()
    {
      this.TableName = this.ToPascalCase(this.List.Name, false);
// Isto não pode estar no código... estas definições têm de passar para um ficheiro de configuração
      this.Connection = new SqlConnection("Server=10.11.30.20;Database=LAKEDB;Persist Security Info=False;User ID=SQL_Server_user;Password=pass");
      this.Connection.Open();
      this.Command = this.Connection.CreateCommand();
      this.Transaction = this.Connection.BeginTransaction(this.TableName + " TXN.");
      this.Command.Connection = this.Connection;
      this.Command.Transaction = this.Transaction;
      this.CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
      this.List.Build();
      this.FNDictionary = new Dictionary<string, Field>((IEqualityComparer<string>) StringComparer.OrdinalIgnoreCase);
      this.BuildDictionary();
      if (!this.TableExists(this.TableName))
        this.CreateTable();
      else
        this.UpdateTableDesign();
    }

    public void DailyUpdate()
    {
      try
      {
        this.Command.CommandText = "DELETE FROM [" + this.TableName + "] WHERE Snapshot = '2100-01-01 00:00:00.000'";
        this.Command.ExecuteNonQuery();
        this.TransferData("2100-01-01 00:00:00.000");
        this.UpdateMetadata();
        this.Transaction.Commit();
      }
      catch (Exception ex)
      {
        Console.WriteLine(ex.Message);
        this.Transaction.Rollback();
      }
      Console.WriteLine("Daily Update done for: " + this.TableName);
    }

    public void CurrentTimeUpdate()
    {
      try
      {
        this.TransferData(this.CurrentTime);
        this.UpdateMetadata();
        this.Transaction.Commit();
      }
      catch (Exception ex)
      {
        Console.WriteLine(ex.Message);
        this.Transaction.Rollback();
      }
      Console.WriteLine(this.CurrentTime + " Update done for: " + this.TableName);
    }

    private void TransferData(string snapDate)
    {
      StringBuilder stringBuilder = new StringBuilder();
      string sqlColNames = this.GetSQLColNames();
      foreach (ListItem listItem in (ClientObjectCollection<ListItem>) this.List.ItemCollection)
      {
        stringBuilder.Clear();
        stringBuilder.AppendLine("INSERT INTO [" + this.TableName + "] " + sqlColNames);
        stringBuilder.Append("VALUES ('" + snapDate + "', ");
        foreach (Field field in this.FNDictionary.Values)
        {
          object obj = listItem[field.InternalName];
          if (obj != null)
          {
            if (obj.GetType() == typeof (FieldLookupValue))
              stringBuilder.Append("'" + (object) ((FieldLookupValue) obj).LookupId + "', ");
            else if (obj.GetType() == typeof (FieldUserValue))
              stringBuilder.Append(((FieldLookupValue) obj).LookupId.ToString() + ", ");
            else if (obj.GetType() == typeof (FieldUrlValue))
              stringBuilder.Append("'" + ((FieldUrlValue) obj).Url + "', ");
            else if (obj.GetType() == typeof (ContentTypeId))
              stringBuilder.Append(obj.ToString() + ", ");
            else if (obj.GetType() == typeof (DateTime))
              stringBuilder.Append(string.Format("'{0:yyyy-MM-dd HH:mm:ss.fff}', ", obj));
            else if (obj.GetType() == typeof (FieldLookupValue[]))
            {
              stringBuilder.Append("'");
              foreach (FieldLookupValue fieldLookupValue in (FieldLookupValue[]) obj)
                stringBuilder.Append(fieldLookupValue.LookupId.ToString() + ";");
              stringBuilder.Append("', ");
            }
            else if (obj.GetType() == typeof (FieldUserValue[]))
            {
              stringBuilder.Append("'");
              foreach (FieldUserValue fieldUserValue in (FieldUserValue[]) obj)
                stringBuilder.Append(fieldUserValue.LookupId.ToString() + ";");
              stringBuilder.Append("', ");
            }
            else
            {
              if (obj.GetType() == typeof (string))
                obj = (object) ((string) obj).Replace("'", "''");
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
          Console.WriteLine("Couldn't insert values: " + ex.Message);
          Console.WriteLine("INSERT STATEMENT: " + stringBuilder.ToString());
        }
      }
    }

    private bool TableExists(string listName)
    {
      this.Command.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '" + listName + "'";
      return (int) this.Command.ExecuteScalar() != 0;
    }

    private void CreateTable()
    {
      StringBuilder stringBuilder = new StringBuilder("CREATE TABLE [");
      stringBuilder.Append(this.TableName);
      stringBuilder.AppendLine("] (");
      stringBuilder.AppendLine("[Snapshot] datetime NULL,");
      foreach (KeyValuePair<string, Field> fn in this.FNDictionary)
      {
        string str = this.SQLFieldType(fn.Value);
        if (str != null)
          stringBuilder.AppendLine("[" + fn.Key + "] " + str + " NULL,");
      }
      stringBuilder.Remove(stringBuilder.Length - 3, 3);
      stringBuilder.Append(")");
      this.Command.CommandText = stringBuilder.ToString();
      try
      {
        this.Command.ExecuteNonQuery();
        Console.WriteLine("Created table " + this.TableName);
      }
      catch (Exception ex)
      {
        Console.WriteLine("Couldn't create table: " + ex.Message);
        Console.WriteLine("CREATE TABLE STATEMENT: " + stringBuilder.ToString());
      }
    }

    private void BuildDictionary()
    {
      foreach (Field field in (ClientObjectCollection<Field>) this.List.Fields)
      {
        if (field.TypeAsString != "Computed")
          this.FNDictionary.Add(this.GetKeyName(this.GetActualColName(field), 1), field);
      }
    }

    private string GetKeyName(string key, int i = 1)
    {
      string key1 = i == 1 ? key : key + (object) i;
      return this.FNDictionary.ContainsKey(key1) ? this.GetKeyName(key, i + 1) : key1;
    }

    private string GetActualColName(Field pField)
    {
      string str = this.ColNameConvetions(pField);
      int num = 0;
      foreach (Field field in (ClientObjectCollection<Field>) this.List.Fields)
      {
        if (field.TypeAsString != "Computed" && str.ToLower() == this.ColNameConvetions(field).ToLower())
          ++num;
      }
      return num > 1 ? this.ToPascalCase(pField.InternalName, true) : str;
    }

    private string ColNameConvetions(Field pField)
    {
      StringBuilder stringBuilder = new StringBuilder(this.ToPascalCase(pField.Title, false));
      string typeAsString = pField.TypeAsString;
      if (!(typeAsString == "Choice"))
      {
        if (!(typeAsString == "User"))
        {
          if (typeAsString == "Lookup" && !pField.FromBaseType)
            stringBuilder.Append("Id");
        }
        else
          stringBuilder.Append("Id");
      }
      else
        stringBuilder.Append("Value");
      return stringBuilder.ToString();
    }

    private string SQLFieldType(Field field)
    {
      string str;
      switch (field.TypeAsString)
      {
        case "Attachments":
        case "Boolean":
          str = "[bit]";
          break;
        case "Calculated":
          str = "[sql_variant]";
          break;
        case "Choice":
        case "File":
        case "LookupMulti":
        case "Note":
        case "Text":
        case "URL":
        case "UserMulti":
          str = "[nvarchar](MAX)";
          break;
        case "ContentTypeId":
          str = "[varbinary](MAX)";
          break;
        case "Counter":
        case "Integer":
        case "ModStat":
        case "User":
          str = "[int]";
          break;
        case "Currency":
        case "Number":
          str = "[float]";
          break;
        case "DateTime":
          str = "[datetime]";
          break;
        case "Guid":
          str = "[uniqueidentifier]";
          break;
        case "Lookup":
          str = field.FromBaseType ? "[nvarchar](MAX)" : "[int]";
          break;
        default:
          Console.WriteLine(field.Title + " gave an error:");
          Console.WriteLine("FOUND A UNKNOWN FIELD TYPE: " + field.TypeAsString + ". PLEASE ADD THIS FIELD TO THE CODE");
          str = (string) null;
          break;
      }
      return str;
    }

    private void UpdateTableDesign()
    {
      foreach (KeyValuePair<string, Field> fn in this.FNDictionary)
      {
        string str1 = this.SQLFieldType(fn.Value);
        int startIndex = str1.IndexOf("[") + 1;
        int num = str1.LastIndexOf("]");
        string str2 = str1.Substring(startIndex, num - startIndex);
        string key = fn.Key;
        this.Command.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '" + this.TableName + "' AND COLUMN_NAME = '" + key + "'";
        if ((int) this.Command.ExecuteScalar() == 0)
        {
          this.Command.CommandText = "ALTER TABLE [" + this.TableName + "] ADD [" + key + "] " + str1;
          this.Command.ExecuteNonQuery();
        }
        else
        {
          this.Command.CommandText = "SELECT [DATA_TYPE] FROM LAKEDB.INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '" + this.TableName + "' AND COLUMN_NAME = '" + key + "'";
          if ((string) this.Command.ExecuteScalar() != str2)
          {
            this.Command.CommandText = "ALTER TABLE [" + this.TableName + "] ALTER COLUMN [" + key + "] " + str1;
            this.Command.ExecuteNonQuery();
          }
        }
      }
    }

    private string GetSQLColNames()
    {
      StringBuilder stringBuilder = new StringBuilder();
      stringBuilder.Append("([Snapshot], ");
      foreach (KeyValuePair<string, Field> fn in this.FNDictionary)
        stringBuilder.Append("[" + fn.Key + "], ");
      stringBuilder.Remove(stringBuilder.Length - 2, 2);
      stringBuilder.Append(")");
      return stringBuilder.ToString();
    }

    private void UpdateMetadata()
    {
      this.Command.CommandText = "DELETE FROM Metadata WHERE TableName = '" + this.TableName + "'";
      this.Command.ExecuteNonQuery();
      this.Command.CommandText = "INSERT INTO Metadata (TableName, LastRefreshDate) Values ('" + this.TableName + "', '" + this.CurrentTime + "')";
      this.Command.ExecuteNonQuery();
    }

    private string ToPascalCase(string pText, bool internalName)
    {
      if (internalName && pText[0] == '_')
        pText += "IN";
      StringBuilder stringBuilder = new StringBuilder();
      foreach (char c in pText)
      {
        if (!char.IsLetterOrDigit(c))
          stringBuilder.Append(" ");
        else
          stringBuilder.Append(c);
      }
      return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(stringBuilder.ToString()).Replace(" ", string.Empty).Replace("X0020", string.Empty).Replace("X003a", string.Empty);
    }
  }
}
