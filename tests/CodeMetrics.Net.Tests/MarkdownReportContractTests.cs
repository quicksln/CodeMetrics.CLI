using CodeMetrics.Cli;
using CodeMetrics.Net.Tests.TestSupport;
using Xunit;

namespace CodeMetrics.Net.Tests;

/// <summary>
/// The markdown report is the format an AI agent reads, so its section headings,
/// summary row labels and member columns are a published contract: parser code
/// is pinned to them. Shapes are asserted verbatim, the way the csv tests do.
/// </summary>
public class MarkdownReportContractTests
{
    private const string MembersHeader =
        "| File | Line | Type | Member | Cognitive | Cyclomatic | LinesOfCode | MaxNesting | Parameters | MaintainabilityIndex |";

    private static string RenderMarkdown(int top = 20, int maxCognitive = 0, int maxCyclomatic = 0)
    {
        var report = ReportFixture.Report(
            ReportFixture.Member(
                memberName: "Run",
                typeName: "Example",
                filePath: "src/Sample.cs",
                lineNumber: 10,
                cognitive: 41,
                cyclomatic: 12,
                linesOfCode: 30,
                maxNestingDepth: 4,
                parameterCount: 3,
                maintainabilityIndex: 18.4d));

        var options = new Options
        {
            Path = "src",
            Format = OutputFormat.Markdown,
            Top = top,
            MaxCognitive = maxCognitive,
            MaxCyclomatic = maxCyclomatic
        };

        return ReportFixture.Render(report, options);
    }

    [Fact]
    public void Markdown_StartsWithTitleAndPath()
    {
        var lines = ReportFixture.Lines(RenderMarkdown());

        Assert.Equal("# Code Metrics Report", lines[0]);
        Assert.Contains("Path: `src`", lines, StringComparer.Ordinal);
    }

    [Fact]
    public void Summary_UsesTheDocumentedTableShape()
    {
        var lines = ReportFixture.Lines(RenderMarkdown());

        Assert.Contains("## Summary", lines, StringComparer.Ordinal);
        Assert.Contains("| Metric | Value |", lines, StringComparer.Ordinal);
        Assert.Contains("| --- | ---: |", lines, StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("Members analysed", "1")]
    [InlineData("C# files", "1")]
    [InlineData("Total lines", "100")]
    [InlineData("C# classes", "1")]
    [InlineData("C# records", "0")]
    [InlineData("C# enums", "0")]
    [InlineData("Analysed LOC", "30")]
    public void Summary_CarriesEveryDocumentedRow(string label, string value)
    {
        // ReportFixture.Report builds SourceSummary(1, 100, 1, 0, 0) and the
        // single member above scores 30 lines of code.
        Assert.Contains($"| {label} | {value} |", ReportFixture.Lines(RenderMarkdown()), StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("Cognitive avg", "41.0")]
    [InlineData("Cognitive median", "41")]
    [InlineData("Cognitive p90", "41")]
    [InlineData("Cognitive max", "41")]
    [InlineData("Cyclomatic avg", "12.0")]
    [InlineData("Cyclomatic max", "12")]
    [InlineData("MI avg", "18.4")]
    [InlineData("MI minimum", "18.4")]
    public void Summary_AveragesUseInvariantDecimalFormatting(string label, string value)
    {
        Assert.Contains($"| {label} | {value} |", ReportFixture.Lines(RenderMarkdown()), StringComparer.Ordinal);
    }

    [Fact]
    public void Summary_AveragesFractionsAcrossMembers()
    {
        var output = ReportFixture.Render(
            OutputFormat.Markdown,
            ReportFixture.Member("Alpha", cognitive: 1),
            ReportFixture.Member("Beta", cognitive: 2));

        Assert.Contains("| Cognitive avg | 1.5 |", ReportFixture.Lines(output), StringComparer.Ordinal);
        Assert.Contains("| Cognitive median | 1 |", ReportFixture.Lines(output), StringComparer.Ordinal);
    }

    [Fact]
    public void Summary_NamesTheHighestRiskAndLowestMaintainabilityMembers()
    {
        var output = ReportFixture.Render(
            OutputFormat.Markdown,
            ReportFixture.Member("Run", typeName: "Example", filePath: "src/Sample.cs", lineNumber: 10, cognitive: 41, cyclomatic: 12),
            ReportFixture.Member("Idle", typeName: "Other", filePath: "src/Quiet.cs", lineNumber: 4, cognitive: 1, maintainabilityIndex: 99.9d));

        var lines = ReportFixture.Lines(output);

        Assert.Contains(lines, line => line.StartsWith("Highest risk member: `Example.Run`", StringComparison.Ordinal)
            && line.Contains("(cognitive 41, cyclomatic 12)", StringComparison.Ordinal)
            && line.Contains("`Sample.cs:10`", StringComparison.Ordinal));

        Assert.Contains(lines, line => line.StartsWith("Lowest maintainability: `Example.Run`", StringComparison.Ordinal)
            && line.Contains("(MI 70.0)", StringComparison.Ordinal));
    }

    [Fact]
    public void RiskBands_ListAllBandsWithRanges()
    {
        var lines = ReportFixture.Lines(RenderMarkdown());

        Assert.Contains("## Risk bands", lines, StringComparer.Ordinal);
        Assert.Contains("| Band | Range | Members |", lines, StringComparer.Ordinal);
        Assert.Contains("| Light | 0-5 | 0 |", lines, StringComparer.Ordinal);
        Assert.Contains("| Moderate | 6-10 | 0 |", lines, StringComparer.Ordinal);
        Assert.Contains("| High | 11-20 | 0 |", lines, StringComparer.Ordinal);
        Assert.Contains("| Severe | 21+ | 1 |", lines, StringComparer.Ordinal);
    }

    [Theory]
    [InlineData(5, "| Light | 0-5 | 1 |", "| Moderate | 6-10 | 0 |")]
    [InlineData(6, "| Moderate | 6-10 | 1 |", "| Light | 0-5 | 0 |")]
    [InlineData(10, "| Moderate | 6-10 | 1 |", "| High | 11-20 | 0 |")]
    [InlineData(11, "| High | 11-20 | 1 |", "| Moderate | 6-10 | 0 |")]
    [InlineData(20, "| High | 11-20 | 1 |", "| Severe | 21+ | 0 |")]
    [InlineData(21, "| Severe | 21+ | 1 |", "| High | 11-20 | 0 |")]
    public void RiskBands_PlaceMembersOnTheDocumentedBoundaries(
        int cognitive,
        string expectedRow,
        string emptyNeighbourRow)
    {
        var lines = ReportFixture.Lines(
            ReportFixture.Render(OutputFormat.Markdown, ReportFixture.Member(cognitive: cognitive)));

        Assert.Contains(expectedRow, lines, StringComparer.Ordinal);
        Assert.Contains(emptyNeighbourRow, lines, StringComparer.Ordinal);
    }

    [Fact]
    public void Members_UsesTheCsvColumnSet()
    {
        Assert.Contains(MembersHeader, ReportFixture.Lines(RenderMarkdown()), StringComparer.Ordinal);
    }

    [Fact]
    public void MemberRow_CarriesEveryColumnInOrder()
    {
        var line = ReportFixture.Lines(RenderMarkdown()).Single(l => l.StartsWith("| src/Sample.cs |", StringComparison.Ordinal));

        Assert.Equal(
            "| src/Sample.cs | 10 | Example | Run | 41 | 12 | 30 | 4 | 3 | 18.4 |",
            line,
            StringComparer.Ordinal);
    }

    [Fact]
    public void MemberRows_RankByCognitiveThenCyclomatic()
    {
        var output = ReportFixture.Render(
            OutputFormat.Markdown,
            ReportFixture.Member("Low", cognitive: 2, cyclomatic: 2),
            ReportFixture.Member("Tied", cognitive: 9, cyclomatic: 4),
            ReportFixture.Member("Top", cognitive: 9, cyclomatic: 11),
            ReportFixture.Member("Middle", cognitive: 6, cyclomatic: 1));

        var files = ReportFixture.Lines(output)
            .Where(line => line.StartsWith("| sample.cs |", StringComparison.Ordinal))
            .Select(line => line.Split('|')[4].Trim())
            .ToList();

        Assert.Equal(["Top", "Tied", "Middle", "Low"], files);
    }

    [Fact]
    public void Members_TopLimitsRowsButNotTotals()
    {
        var report = ReportFixture.Report(
            ReportFixture.Member("A", cognitive: 30),
            ReportFixture.Member("B", cognitive: 20),
            ReportFixture.Member("C", cognitive: 10));

        var output = ReportFixture.Render(report, new Options
        {
            Path = "src",
            Format = OutputFormat.Markdown,
            Top = 2
        });

        var lines = ReportFixture.Lines(output);

        Assert.Contains("Showing 2 of 3 member(s).", string.Join("\n", lines), StringComparison.Ordinal);
        Assert.Equal(2, lines.Count(line => line.StartsWith("| sample.cs |", StringComparison.Ordinal)));

        // The aggregates still describe the whole population.
        Assert.Contains("| Members analysed | 3 |", lines, StringComparer.Ordinal);
        Assert.Contains("| Severe | 21+ | 1 |", lines, StringComparer.Ordinal);
        Assert.Contains("| High | 11-20 | 1 |", lines, StringComparer.Ordinal);
        Assert.Contains("| Moderate | 6-10 | 1 |", lines, StringComparer.Ordinal);
    }

    [Fact]
    public void Members_TopZeroPrintsEveryMember()
    {
        var report = ReportFixture.Report(
            ReportFixture.Member("A", cognitive: 3),
            ReportFixture.Member("B", cognitive: 2),
            ReportFixture.Member("C", cognitive: 1));

        var output = ReportFixture.Render(report, new Options { Path = "src", Format = OutputFormat.Markdown, Top = 0 });

        Assert.Equal(3, ReportFixture.Lines(output).Count(line => line.StartsWith("| sample.cs |", StringComparison.Ordinal)));
    }

    [Fact]
    public void Members_PipeInANameCannotBreakTheTable()
    {
        var output = ReportFixture.Render(OutputFormat.Markdown, ReportFixture.Member(memberName: "A|B"));

        Assert.Contains("| A\\|B |", output, StringComparison.Ordinal);
        Assert.DoesNotContain("| A|B |", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Gate_IsOmittedWhenNoThresholdIsConfigured()
    {
        Assert.DoesNotContain("## Gate", ReportFixture.Lines(RenderMarkdown()), StringComparer.Ordinal);
    }

    [Fact]
    public void Gate_CountsBreachesPerConfiguredLimit()
    {
        var lines = ReportFixture.Lines(RenderMarkdown(maxCognitive: 20, maxCyclomatic: 10));

        Assert.Contains("## Gate", lines, StringComparer.Ordinal);
        Assert.Contains("Gate cognitive <= 20: 1 member(s) over the limit.", lines, StringComparer.Ordinal);
        Assert.Contains("Gate cyclomatic <= 10: 1 member(s) over the limit.", lines, StringComparer.Ordinal);
    }

    [Fact]
    public void Gate_PrintsOnlyTheConfiguredLimit()
    {
        var lines = ReportFixture.Lines(RenderMarkdown(maxCognitive: 20));

        Assert.Contains("Gate cognitive <= 20: 1 member(s) over the limit.", lines, StringComparer.Ordinal);
        Assert.DoesNotContain("Gate cyclomatic", lines, StringComparer.Ordinal);
    }

    [Fact]
    public void EmptyReport_PrintsZerosWithoutThrowing()
    {
        var output = ReportFixture.Render(
            ReportFixture.Report(),
            new Options { Path = "src", Format = OutputFormat.Markdown });

        var lines = ReportFixture.Lines(output);

        Assert.Contains("| Members analysed | 0 |", lines, StringComparer.Ordinal);
        Assert.Contains("| Cognitive avg | 0.0 |", lines, StringComparer.Ordinal);
        Assert.Contains("| Cognitive max | 0 |", lines, StringComparer.Ordinal);
        Assert.Contains("No members with a body were found.", lines, StringComparer.Ordinal);
        Assert.Equal(0, lines.Count(line => line.StartsWith("| sample.cs |", StringComparison.Ordinal)));
    }

    [Fact]
    public void Sections_AppearInDocumentedOrder()
    {
        var lines = ReportFixture.Lines(RenderMarkdown(maxCognitive: 20));
        var headings = lines.Where(line => line.StartsWith("## ", StringComparison.Ordinal)).ToList();

        Assert.Equal(["## Summary", "## Risk bands", "## Members", "## Gate"], headings);
    }
}
