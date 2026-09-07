using CodeMetrics.Analysis;
using CodeMetrics.Cli;
using CodeMetrics.Net.Tests.TestSupport;
using Xunit;

namespace CodeMetrics.Net.Tests;

/// <summary>
/// The csv header, its column order, the escaping rules and the numeric format
/// are a published contract: spreadsheets and scripts are pinned to them.
/// </summary>
public class CsvReportContractTests
{
    private const string Header =
        "File,Line,Type,Member,Cognitive,Cyclomatic,LinesOfCode,MaxNesting,Parameters,MaintainabilityIndex";

    [Fact]
    public void Csv_StartsWithTheDocumentedHeader()
    {
        Assert.Equal(Header, ReportFixture.Lines(Render())[0]);
    }

    [Fact]
    public void Row_CarriesTheColumnsInHeaderOrder()
    {
        var member = ReportFixture.Member(
            memberName: "Run",
            typeName: "Example",
            filePath: "src/Sample.cs",
            lineNumber: 10,
            cognitive: 4,
            cyclomatic: 3,
            linesOfCode: 6,
            maxNestingDepth: 2,
            parameterCount: 1,
            maintainabilityIndex: 70.5);

        var row = ReportFixture.Lines(Render(member))[1];

        Assert.Equal("src/Sample.cs,10,Example,Run,4,3,6,2,1,70.5", row);
    }

    [Fact]
    public void Rows_AreOrderedByCognitiveComplexityDescending()
    {
        var output = Render(
            ReportFixture.Member("Low", cognitive: 1),
            ReportFixture.Member("High", cognitive: 9),
            ReportFixture.Member("Middle", cognitive: 5));

        var names = ReportFixture.Lines(output)
            .Skip(1)
            .Select(line => line.Split(',')[3])
            .ToArray();

        Assert.Equal(["High", "Middle", "Low"], names);
    }

    [Theory]
    // A value with a separator or a quote in it has to be quoted, or the row
    // gains a column.
    [InlineData("plain.cs", "plain.cs")]
    [InlineData("a,b.cs", "\"a,b.cs\"")]
    [InlineData("say \"hi\".cs", "\"say \"\"hi\"\".cs\"")]
    public void Values_AreQuotedOnlyWhenTheyNeedToBe(string filePath, string expected)
    {
        var row = ReportFixture.Lines(Render(ReportFixture.Member(filePath: filePath)))[1];

        Assert.StartsWith(expected + ",", row, StringComparison.Ordinal);
    }

    [Fact]
    public void MaintainabilityIndex_UsesOneDecimalPlace()
    {
        var output = Render(ReportFixture.Member(maintainabilityIndex: 66.6666));
        var row = ReportFixture.Lines(output)[1];

        Assert.EndsWith(",66.7", row, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryRowHasAsManyFieldsAsTheHeader()
    {
        // Guards a property added to MemberMetrics without a matching column.
        var expectedColumns = Header.Split(',').Length;
        var output = Render(ReportFixture.Member(), ReportFixture.Member("Other", cognitive: 1));

        foreach (var row in ReportFixture.Lines(output).Skip(1))
        {
            Assert.Equal(expectedColumns, CountFields(row));
        }
    }

    [Fact]
    public void EmptyReport_StillWritesTheHeader()
    {
        // A consumer must be able to read a clean run without special-casing it.
        Assert.Equal([Header], ReportFixture.Lines(Render()));
    }

    private static string Render(params MemberMetrics[] members) =>
        ReportFixture.Render(OutputFormat.Csv, members);

    /// <summary>Counts csv fields, honouring quoted sections.</summary>
    private static int CountFields(string row)
    {
        var fields = 1;
        var quoted = false;

        foreach (var character in row)
        {
            if (character == '"')
            {
                quoted = !quoted;
            }
            else if (character == ',' && !quoted)
            {
                fields++;
            }
        }

        return fields;
    }
}
