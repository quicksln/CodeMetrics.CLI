using CodeMetrics.Analysis;
using CodeMetrics.Cli;
using CodeMetrics.Net.Tests.TestSupport;
using Xunit;

namespace CodeMetrics.Net.Tests;

/// <summary>
/// The table is what a developer reads at the terminal. Its job is to rank the
/// worst members first and summarise the rest, so the ranking, the row budget,
/// the band boundaries and the gate reporting are what these tests pin.
/// </summary>
public class TableReportTests
{
    [Fact]
    public void Header_CountsFilesFromTheSourceSummaryAndMembersFromTheReport()
    {
        var report = new AnalysisReport(
            [ReportFixture.Member("A"), ReportFixture.Member("B"), ReportFixture.Member("C")],
            new SourceSummary(4, 100, 3, 0, 0));

        var output = ReportFixture.Render(report, new Options { Path = "src" });

        Assert.Contains("Analysed 4 file(s), 3 member(s).", output, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyReport_SaysSoWithoutThrowing()
    {
        // WriteSummary takes Max() of the member scores unguarded, so the early
        // return here is the only thing keeping an empty run from crashing.
        var report = new AnalysisReport([], new SourceSummary(2, 40, 0, 0, 0));

        var output = ReportFixture.Render(report, new Options { Path = "src" });

        Assert.Contains("Analysed 2 file(s), 0 member(s).", output, StringComparison.Ordinal);
        Assert.Contains("No members with a body were found.", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Cognitive bands", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Columns_CarryTheDocumentedHeadings()
    {
        var headings = HeaderRow(Render(new Options { Path = "src" }, ReportFixture.Member()));

        Assert.Equal(["Cog", "Cyc", "LOC", "Nest", "MI", "Member", "Location"], headings);
    }

    [Fact]
    public void Rows_RankByCognitiveThenCyclomaticComplexity()
    {
        var output = Render(
            new Options { Path = "src" },
            ReportFixture.Member("Low", cognitive: 1, cyclomatic: 1),
            ReportFixture.Member("TiedLower", cognitive: 5, cyclomatic: 2),
            ReportFixture.Member("TiedHigher", cognitive: 5, cyclomatic: 9),
            ReportFixture.Member("Worst", cognitive: 12, cyclomatic: 1));

        // The Member column carries the fully qualified name.
        Assert.Equal(
            ["Example.Worst", "Example.TiedHigher", "Example.TiedLower", "Example.Low"],
            RankedMembers(output));
    }

    [Fact]
    public void Top_LimitsPrintedRowsButNotTheSummary()
    {
        var members = Enumerable.Range(1, 5)
            .Select(index => ReportFixture.Member($"M{index}", cognitive: index, linesOfCode: 10))
            .ToArray();

        var output = Render(new Options { Path = "src", Top = 2 }, members);

        Assert.Equal(["Example.M5", "Example.M4"], RankedMembers(output));

        // The header and the totals still describe every member analysed.
        Assert.Contains("5 member(s).", output, StringComparison.Ordinal);
        Assert.Contains("Total LOC    50", output, StringComparison.Ordinal);
    }

    [Fact]
    public void LongMemberNames_AreTruncatedFromTheFrontToKeepTheLeafName()
    {
        // The tail identifies the member; the namespace prefix is noise.
        var typeName = new string('N', 60);
        var output = Render(new Options { Path = "src" }, ReportFixture.Member("Run", typeName));

        var cell = RankedMembers(output)[0];

        Assert.Equal(48, cell.Length);
        Assert.StartsWith("...", cell, StringComparison.Ordinal);
        Assert.EndsWith("N.Run", cell, StringComparison.Ordinal);
    }

    [Fact]
    public void Location_IsFileNameAndLineNotTheFullPath()
    {
        var output = Render(
            new Options { Path = "src" },
            ReportFixture.Member(filePath: Path.Combine("deep", "nested", "Sample.cs"), lineNumber: 42));

        Assert.Contains("Sample.cs:42", output, StringComparison.Ordinal);
        Assert.DoesNotContain(Path.Combine("deep", "nested"), output, StringComparison.Ordinal);
    }

    [Fact]
    public void NumericColumns_AreRightAligned()
    {
        var output = Render(
            new Options { Path = "src" },
            ReportFixture.Member("Wide", cognitive: 100),
            ReportFixture.Member("Narrow", cognitive: 1));

        var rows = DataRows(output);

        // Both cognitive cells occupy the same width, padded on the left.
        Assert.StartsWith("100", rows[0], StringComparison.Ordinal);
        Assert.StartsWith("  1", rows[1], StringComparison.Ordinal);
    }

    [Fact]
    public void Legend_ExplainsEveryAbbreviatedColumn()
    {
        var output = Render(new Options { Path = "src" }, ReportFixture.Member());

        Assert.Contains("Cog = Cognitive complexity", output, StringComparison.Ordinal);
        Assert.Contains("Cyc = Cyclomatic complexity", output, StringComparison.Ordinal);
        Assert.Contains("MI = Maintainability index", output, StringComparison.Ordinal);
    }

    [Fact]
    public void CognitiveBands_CountAtTheDocumentedBoundaries()
    {
        // One member on each side of every boundary the legend advertises.
        var members = new[] { 0, 5, 6, 10, 11, 20, 21 }
            .Select((score, index) => ReportFixture.Member($"M{index}", cognitive: score))
            .ToArray();

        var output = Render(new Options { Path = "src", Top = 100 }, members);

        Assert.Contains(
            "Cognitive bands   simple(0-5) 2   moderate(6-10) 2   high(11-20) 2   severe(21+) 1",
            output,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Summary_ReportsAverageMedianP90AndMax()
    {
        var members = new[] { 1, 2, 3, 4, 10 }
            .Select((score, index) => ReportFixture.Member($"M{index}", cognitive: score, cyclomatic: 1))
            .ToArray();

        var output = Render(new Options { Path = "src" }, members);
        var line = ReportFixture.Lines(output)
            .Single(text => text.StartsWith("Cognitive    avg", StringComparison.Ordinal));

        // avg 4.0; nearest-rank median of 5 values is the 3rd, p90 is the 5th.
        Assert.Contains("avg   4.0", line, StringComparison.Ordinal);
        Assert.Contains("median    3", line, StringComparison.Ordinal);
        Assert.Contains("p90   10", line, StringComparison.Ordinal);
        Assert.Contains("max   10", line, StringComparison.Ordinal);
    }

    [Fact]
    public void GateLines_AreOmittedWhenNoGateIsConfigured()
    {
        var output = Render(new Options { Path = "src" }, ReportFixture.Member());

        Assert.DoesNotContain("Gate ", output, StringComparison.Ordinal);
    }

    [Fact]
    public void GateLines_CountOnlyMembersStrictlyOverTheLimit()
    {
        // The exit code treats a member exactly at the limit as clean, so the
        // report must agree with it.
        var options = new Options { Path = "src", MaxCognitive = 5, MaxCyclomatic = 7 };
        var output = Render(
            options,
            ReportFixture.Member("AtLimit", cognitive: 5, cyclomatic: 7),
            ReportFixture.Member("Over", cognitive: 6, cyclomatic: 8));

        Assert.Contains("Gate cognitive  <= 5: 1 member(s) over the limit.", output, StringComparison.Ordinal);
        Assert.Contains("Gate cyclomatic <= 7: 1 member(s) over the limit.", output, StringComparison.Ordinal);
    }

    [Fact]
    public void GateLines_AppearOnlyForTheGateThatWasSet()
    {
        var output = Render(
            new Options { Path = "src", MaxCognitive = 5 },
            ReportFixture.Member(cognitive: 9, cyclomatic: 9));

        Assert.Contains("Gate cognitive", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Gate cyclomatic", output, StringComparison.Ordinal);
    }

    private static string Render(Options options, params MemberMetrics[] members) =>
        ReportFixture.Render(ReportFixture.Report(members), options);

    private static string[] HeaderRow(string output) => SplitCells(TableLines(output)[0]);

    private static string[] DataRows(string output) => TableLines(output).Skip(2).ToArray();

    private static string[] RankedMembers(string output) =>
        DataRows(output).Select(row => SplitCells(row)[5]).ToArray();

    /// <summary>The header, its underline and the ranked rows, with the surrounding prose dropped.</summary>
    private static string[] TableLines(string output) =>
        ReportFixture.Lines(output)
            .SkipWhile(line => !line.StartsWith("Cog", StringComparison.Ordinal))
            .TakeWhile(line => line.Length > 0)
            .ToArray();

    private static string[] SplitCells(string row) =>
        row.Split("  ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
