using System;
using System.Collections.Generic;
using System.Linq;

namespace SPO2SQL.SharePoint;

public static class CamlQueryBuilder
{
    public static string BuildNullFieldQuery(string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            throw new ArgumentException("Field name cannot be empty.", nameof(fieldName));
        }

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

    public static string BuildNotNullFieldQuery(string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            throw new ArgumentException("Field name cannot be empty.", nameof(fieldName));
        }

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

    public static string BuildDateRangeQuery(string fieldName, DateTime from, DateTime to)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            throw new ArgumentException("Field name cannot be empty.", nameof(fieldName));
        }

        if (to < from)
        {
            throw new ArgumentException("'to' date must be greater than or equal to 'from' date.");
        }

        var fromValue = EscapeXmlValue(from.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"));
        var toValue = EscapeXmlValue(to.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"));

        return $@"<View>
    <Query>
        <Where>
            <And>
                <Geq>
                    <FieldRef Name='{EscapeXmlValue(fieldName)}' />
                    <Value Type='DateTime' IncludeTimeValue='TRUE'>{fromValue}</Value>
                </Geq>
                <Leq>
                    <FieldRef Name='{EscapeXmlValue(fieldName)}' />
                    <Value Type='DateTime' IncludeTimeValue='TRUE'>{toValue}</Value>
                </Leq>
            </And>
        </Where>
    </Query>
</View>";
    }

    public static string BuildEqualTextQuery(string fieldName, string value)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            throw new ArgumentException("Field name cannot be empty.", nameof(fieldName));
        }

        ArgumentNullException.ThrowIfNull(value);

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

    public static string BuildContainsTextQuery(string fieldName, string value)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            throw new ArgumentException("Field name cannot be empty.", nameof(fieldName));
        }

        ArgumentNullException.ThrowIfNull(value);

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

    public static string BuildEqualNumberQuery(string fieldName, decimal value)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            throw new ArgumentException("Field name cannot be empty.", nameof(fieldName));
        }

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

    public static string BuildAndQuery(params string[] fieldConditions)
    {
        if (fieldConditions == null || fieldConditions.Length == 0)
        {
            throw new ArgumentException("At least one condition is required.", nameof(fieldConditions));
        }

        if (fieldConditions.Length == 1)
        {
            return fieldConditions[0];
        }

        var conditions = string.Join("", fieldConditions.Take(fieldConditions.Length - 1)
            .Select(c => $"<And>{c}"));

        var where = fieldConditions[fieldConditions.Length - 1];
        for (int i = 0; i < fieldConditions.Length - 1; i++)
        {
            where += "</And>";
        }

        return $@"<View>
    <Query>
        <Where>
            {conditions}{where}
        </Where>
    </Query>
</View>";
    }

    public static string BuildOrQuery(params string[] fieldConditions)
    {
        if (fieldConditions == null || fieldConditions.Length == 0)
        {
            throw new ArgumentException("At least one condition is required.", nameof(fieldConditions));
        }

        if (fieldConditions.Length == 1)
        {
            return fieldConditions[0];
        }

        var conditions = string.Join("", fieldConditions.Take(fieldConditions.Length - 1)
            .Select(c => $"<Or>{c}"));

        var where = fieldConditions[fieldConditions.Length - 1];
        for (int i = 0; i < fieldConditions.Length - 1; i++)
        {
            where += "</Or>";
        }

        return $@"<View>
    <Query>
        <Where>
            {conditions}{where}
        </Where>
    </Query>
</View>";
    }

    public static string BuildOrderedQuery(string whereClause, string orderByField, bool ascending = true)
    {
        if (string.IsNullOrWhiteSpace(whereClause))
        {
            throw new ArgumentException("Where clause cannot be empty.", nameof(whereClause));
        }

        if (string.IsNullOrWhiteSpace(orderByField))
        {
            throw new ArgumentException("Order by field cannot be empty.", nameof(orderByField));
        }

        var orderDirection = ascending ? "TRUE" : "FALSE";

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

    public static string BuildLimitedQuery(string whereClause, int rowLimit)
    {
        if (string.IsNullOrWhiteSpace(whereClause))
        {
            throw new ArgumentException("Where clause cannot be empty.", nameof(whereClause));
        }

        if (rowLimit <= 0)
        {
            throw new ArgumentException("Row limit must be greater than 0.", nameof(rowLimit));
        }

        return $@"<View>
    <Query>
        <Where>
            {ExtractWhereContent(whereClause)}
        </Where>
    </Query>
    <RowLimit>{rowLimit}</RowLimit>
</View>";
    }

    public static string EscapeXmlValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    private static string ExtractWhereContent(string query)
    {
        const string whereStart = "<Where>";
        const string whereEnd = "</Where>";

        var startIndex = query.IndexOf(whereStart, StringComparison.OrdinalIgnoreCase);
        var endIndex = query.IndexOf(whereEnd, StringComparison.OrdinalIgnoreCase);

        if (startIndex < 0 || endIndex < 0)
        {
            return query;
        }

        return query.Substring(startIndex + whereStart.Length, endIndex - startIndex - whereStart.Length);
    }
}
