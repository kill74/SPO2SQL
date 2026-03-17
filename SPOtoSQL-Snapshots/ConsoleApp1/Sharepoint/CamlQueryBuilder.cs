using System;
using System.Collections.Generic;
using System.Linq;

namespace Bring.Sharepoint
{
  /// <summary>
  /// Helper class for building CAML (Collaborative Application Markup Language) queries for SharePoint.
  /// Provides type-safe methods to construct common query patterns.
  /// </summary>
  public static class CamlQueryBuilder
  {
    /// <summary>
    /// Builds a CAML query to find items where a field is NULL.
    /// </summary>
    public static string BuildNullFieldQuery(string fieldName)
    {
      if (string.IsNullOrWhiteSpace(fieldName))
        throw new ArgumentException("Field name cannot be empty.", nameof(fieldName));

      return $@"<View>
    <Query>
        <Where>
            <IsNull>
                <FieldRef Name='{EscapeXmlValue(fieldName)}' />
            </IsNull>
        </Where>
    </Query>
</View>";
    }

    /// <summary>
    /// Builds a CAML query to find items where a field is NOT NULL.
    /// </summary>
    public static string BuildNotNullFieldQuery(string fieldName)
    {
      if (string.IsNullOrWhiteSpace(fieldName))
        throw new ArgumentException("Field name cannot be empty.", nameof(fieldName));

      return $@"<View>
    <Query>
        <Where>
            <IsNotNull>
                <FieldRef Name='{EscapeXmlValue(fieldName)}' />
            </IsNotNull>
        </Where>
    </Query>
</View>";
    }

    /// <summary>
    /// Builds a CAML query to find items where a date field is within a specified range.
    /// </summary>
    public static string BuildDateRangeQuery(string fieldName, DateTime from, DateTime to)
    {
      if (string.IsNullOrWhiteSpace(fieldName))
        throw new ArgumentException("Field name cannot be empty.", nameof(fieldName));

      if (to < from)
        throw new ArgumentException("'to' date must be greater than or equal to 'from' date.");

      var fromValue = EscapeXmlValue(from.ToString("yyyy-MM-dd HH:mm:ss"));
      var toValue = EscapeXmlValue(to.ToString("yyyy-MM-dd HH:mm:ss"));

      return $@"<View>
    <Query>
        <Where>
            <And>
                <Geq>
                    <FieldRef Name='{EscapeXmlValue(fieldName)}' />
                    <Value Type='DateTime'>{fromValue}</Value>
                </Geq>
                <Leq>
                    <FieldRef Name='{EscapeXmlValue(fieldName)}' />
                    <Value Type='DateTime'>{toValue}</Value>
                </Leq>
            </And>
        </Where>
    </Query>
</View>";
    }

    /// <summary>
    /// Builds a CAML query to find items where a text field equals a specific value.
    /// </summary>
    public static string BuildEqualTextQuery(string fieldName, string value)
    {
      if (string.IsNullOrWhiteSpace(fieldName))
        throw new ArgumentException("Field name cannot be empty.", nameof(fieldName));

      if (value == null)
        throw new ArgumentNullException(nameof(value));

      return $@"<View>
    <Query>
        <Where>
            <Eq>
                <FieldRef Name='{EscapeXmlValue(fieldName)}' />
                <Value Type='Text'>{EscapeXmlValue(value)}</Value>
            </Eq>
        </Where>
    </Query>
</View>";
    }

    /// <summary>
    /// Builds a CAML query to find items where a text field contains a specific substring.
    /// </summary>
    public static string BuildContainsTextQuery(string fieldName, string value)
    {
      if (string.IsNullOrWhiteSpace(fieldName))
        throw new ArgumentException("Field name cannot be empty.", nameof(fieldName));

      if (value == null)
        throw new ArgumentNullException(nameof(value));

      return $@"<View>
    <Query>
        <Where>
            <Contains>
                <FieldRef Name='{EscapeXmlValue(fieldName)}' />
                <Value Type='Text'>{EscapeXmlValue(value)}</Value>
            </Contains>
        </Where>
    </Query>
</View>";
    }

    /// <summary>
    /// Builds a CAML query to find items where a numeric field equals a specific value.
    /// </summary>
    public static string BuildEqualNumberQuery(string fieldName, decimal value)
    {
      if (string.IsNullOrWhiteSpace(fieldName))
        throw new ArgumentException("Field name cannot be empty.", nameof(fieldName));

      return $@"<View>
    <Query>
        <Where>
            <Eq>
                <FieldRef Name='{EscapeXmlValue(fieldName)}' />
                <Value Type='Number'>{EscapeXmlValue(value.ToString())}</Value>
            </Eq>
        </Where>
    </Query>
</View>";
    }

    /// <summary>
    /// Builds a CAML query with multiple AND conditions. All conditions must be true.
    /// </summary>
    public static string BuildAndQuery(params string[] fieldConditions)
    {
      if (fieldConditions == null || fieldConditions.Length == 0)
        throw new ArgumentException("At least one condition is required.", nameof(fieldConditions));

      if (fieldConditions.Length == 1)
        return fieldConditions[0];

      var conditions = string.Join("", fieldConditions.Take(fieldConditions.Length - 1)
          .Select(c => $"<And>{c}"));

      var where = fieldConditions[fieldConditions.Length - 1];
      for (int i = 0; i < fieldConditions.Length - 1; i++)
        where += "</And>";

      return $@"<View>
    <Query>
        <Where>
            {conditions}{where}
        </Where>
    </Query>
</View>";
    }

    /// <summary>
    /// Builds a CAML query with multiple OR conditions. At least one condition must be true.
    /// </summary>
    public static string BuildOrQuery(params string[] fieldConditions)
    {
      if (fieldConditions == null || fieldConditions.Length == 0)
        throw new ArgumentException("At least one condition is required.", nameof(fieldConditions));

      if (fieldConditions.Length == 1)
        return fieldConditions[0];

      var conditions = string.Join("", fieldConditions.Take(fieldConditions.Length - 1)
          .Select(c => $"<Or>{c}"));

      var where = fieldConditions[fieldConditions.Length - 1];
      for (int i = 0; i < fieldConditions.Length - 1; i++)
        where += "</Or>";

      return $@"<View>
    <Query>
        <Where>
            {conditions}{where}
        </Where>
    </Query>
</View>";
    }

    /// <summary>
    /// Builds a CAML query ordered by specified field.
    /// </summary>
    public static string BuildOrderedQuery(string whereClause, string orderByField, bool ascending = true)
    {
      if (string.IsNullOrWhiteSpace(whereClause))
        throw new ArgumentException("Where clause cannot be empty.", nameof(whereClause));

      if (string.IsNullOrWhiteSpace(orderByField))
        throw new ArgumentException("Order by field cannot be empty.", nameof(orderByField));

      var orderDirection = ascending ? "Ascending" : "Descending";

      return $@"<View>
    <Query>
        <Where>
            {ExtractWhereContent(whereClause)}
        </Where>
        <OrderBy>
            <FieldRef Name='{EscapeXmlValue(orderByField)}' Ascending='{orderDirection}' />
        </OrderBy>
    </Query>
</View>";
    }

    /// <summary>
    /// Builds a CAML query with row limit.
    /// </summary>
    public static string BuildLimitedQuery(string whereClause, int rowLimit)
    {
      if (string.IsNullOrWhiteSpace(whereClause))
        throw new ArgumentException("Where clause cannot be empty.", nameof(whereClause));

      if (rowLimit <= 0)
        throw new ArgumentException("Row limit must be greater than 0.", nameof(rowLimit));

      return $@"<View>
    <Query>
        <Where>
            {ExtractWhereContent(whereClause)}
        </Where>
    </Query>
    <RowLimit>{rowLimit}</RowLimit>
</View>";
    }

    /// <summary>
    /// Escapes XML special characters in values to prevent injection and XML parsing errors.
    /// </summary>
    /// <param name="value">The value to escape.</param>
    /// <returns>The escaped value safe for XML.</returns>
    public static string EscapeXmlValue(string value)
    {
      if (string.IsNullOrEmpty(value))
        return value;

      return value
          .Replace("&", "&amp;")
          .Replace("<", "&lt;")
          .Replace(">", "&gt;")
          .Replace("\"", "&quot;")
          .Replace("'", "&apos;");
    }

    /// <summary>
    /// Extracts the Where content from a full CAML query (removes outer View and Query tags).
    /// Used internally for composing complex queries.
    /// </summary>
    private static string ExtractWhereContent(string query)
    {
      const string whereStart = "<Where>";
      const string whereEnd = "</Where>";

      var startIndex = query.IndexOf(whereStart, StringComparison.OrdinalIgnoreCase);
      var endIndex = query.IndexOf(whereEnd, StringComparison.OrdinalIgnoreCase);

      if (startIndex < 0 || endIndex < 0)
        return query; // Return as-is if not proper format

      return query.Substring(startIndex + whereStart.Length, endIndex - startIndex - whereStart.Length);
    }
  }
}
