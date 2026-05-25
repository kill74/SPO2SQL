using FluentAssertions;
using SPO2SQL.SharePoint;
using Xunit;

namespace SPO2SQL.Core.Tests;

public class CamlQueryBuilderTests
{
    [Fact]
    public void BuildNullFieldQuery_WithValidFieldName_ReturnsValidCaml()
    {
        var result = CamlQueryBuilder.BuildNullFieldQuery("ApprovalStatus");

        result.Should().Contain("<IsNull>")
              .And.Contain("<FieldRef Name='ApprovalStatus' />")
              .And.Contain("<View>")
              .And.Contain("</View>");
    }

    [Fact]
    public void BuildNullFieldQuery_WithEmptyFieldName_ThrowsArgumentException()
    {
        var act = () => CamlQueryBuilder.BuildNullFieldQuery("");

        act.Should().Throw<ArgumentException>()
           .WithParameterName("fieldName");
    }

    [Fact]
    public void BuildDateRangeQuery_WithValidDates_ReturnsValidCaml()
    {
        var from = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        var result = CamlQueryBuilder.BuildDateRangeQuery("Created", from, to);

        result.Should().Contain("<Geq>")
              .And.Contain("<Leq>")
              .And.Contain("<FieldRef Name='Created' />")
              .And.Contain("<And>");
    }

    [Fact]
    public void BuildDateRangeQuery_WithInvalidRange_ThrowsArgumentException()
    {
        var from = DateTime.UtcNow;
        var to = from.AddDays(-1);

        var act = () => CamlQueryBuilder.BuildDateRangeQuery("Modified", from, to);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void BuildAndQuery_WithMultipleConditions_ReturnsNestedAnd()
    {
        var cond1 = "<Eq><FieldRef Name='Status' /><Value Type='Text'>Active</Value></Eq>";
        var cond2 = "<IsNotNull><FieldRef Name='Owner' /></IsNotNull>";

        var result = CamlQueryBuilder.BuildAndQuery(cond1, cond2);

        result.Should().Contain("<And>")
              .And.Contain("</And>");
    }

    [Fact]
    public void BuildOrQuery_WithMultipleConditions_ReturnsNestedOr()
    {
        var cond1 = "<Eq><FieldRef Name='Status' /><Value Type='Text'>Active</Value></Eq>";
        var cond2 = "<Eq><FieldRef Name='Status' /><Value Type='Text'>Pending</Value></Eq>";

        var result = CamlQueryBuilder.BuildOrQuery(cond1, cond2);

        result.Should().Contain("<Or>")
              .And.Contain("</Or>");
    }

    [Fact]
    public void BuildEqualTextQuery_WithValidInputs_ReturnsValidCaml()
    {
        var result = CamlQueryBuilder.BuildEqualTextQuery("Department", "Sales");

        result.Should().Contain("<Eq>")
              .And.Contain("<FieldRef Name='Department' />")
              .And.Contain("<Value Type='Text'>Sales</Value>");
    }

    [Fact]
    public void EscapeXmlValue_WithSpecialChars_ReturnsEscapedString()
    {
        var result = CamlQueryBuilder.EscapeXmlValue("A&B < C > D \"test\" 'done'");

        result.Should().Be("A&amp;B &lt; C &gt; D &quot;test&quot; &apos;done&apos;");
    }

    [Fact]
    public void BuildOrderedQuery_WithAscending_ReturnsAscendingOrder()
    {
        var where = CamlQueryBuilder.BuildNotNullFieldQuery("Title");

        var result = CamlQueryBuilder.BuildOrderedQuery(where, "Created", ascending: true);

        result.Should().Contain("Ascending='TRUE'");
    }

    [Fact]
    public void BuildLimitedQuery_WithPositiveLimit_ReturnsRowLimit()
    {
        var where = CamlQueryBuilder.BuildNotNullFieldQuery("Title");

        var result = CamlQueryBuilder.BuildLimitedQuery(where, 100);

        result.Should().Contain("<RowLimit>100</RowLimit>");
    }
}
