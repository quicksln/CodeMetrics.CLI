using CodeMetrics.Analysis;
using CodeMetrics.Net.Tests.TestSupport;
using Xunit;

namespace CodeMetrics.Net.Tests;

/// <summary>
/// The shapes that travel between analysis and reporting. The null summary on
/// a skipped file is load-bearing: it is what keeps an unreadable file out of
/// the totals instead of counting as an empty one.
/// </summary>
public class AnalysisModelTests
{
    [Fact]
    public void SkippedFile_HasNoMembersAndNoSummary()
    {
        var result = FileAnalysisResult.Skipped("locked.cs");

        Assert.Equal("locked.cs", result.FilePath);
        Assert.Empty(result.Members);
        Assert.Null(result.SourceSummary);
    }

    [Fact]
    public void SuccessfulFile_CarriesItsMembersAndSummary()
    {
        var members = new[] { ReportFixture.Member() };
        var summary = new SourceSummary(1, 20, 1, 0, 0);

        var result = FileAnalysisResult.Successful("widget.cs", members, summary);

        Assert.Equal("widget.cs", result.FilePath);
        Assert.Equal(members, result.Members);
        Assert.Equal(summary, result.SourceSummary);
    }

    [Fact]
    public void FullName_JoinsTheTypeAndMemberNames()
    {
        Assert.Equal("Example.Run", ReportFixture.Member("Run", "Example").FullName);
    }

    [Fact]
    public void FullName_ReadsSensiblyForTopLevelStatements()
    {
        Assert.Equal("<top-level>.<main>", ReportFixture.Member("<main>", "<top-level>").FullName);
    }

    [Fact]
    public void AnalyzedFile_CountsItsTypesAndPhysicalLines()
    {
        const string source = """
            namespace Example;

            class First { }

            class Second { }

            record Third(int Value);

            enum Fourth { A, B }
            """;

        var result = AnalyzerFixture.AnalyzeResult(source);
        var summary = result.SourceSummary;

        Assert.NotNull(summary);
        Assert.Equal(1, summary.FileCount);
        Assert.Equal(2, summary.ClassCount);
        Assert.Equal(1, summary.RecordCount);
        Assert.Equal(1, summary.EnumCount);

        // Physical lines, blank ones included.
        Assert.Equal(9, summary.LineCount);
    }

    [Fact]
    public void EmptySummary_IsTheIdentityForCombine()
    {
        var summary = new SourceSummary(1, 20, 2, 1, 1);

        Assert.Equal(summary, SourceSummary.Empty.Combine(summary));
        Assert.Equal(summary, summary.Combine(SourceSummary.Empty));
    }

    [Fact]
    public void Combine_IsAssociative()
    {
        // Files are folded in parallel, so grouping must not change the totals.
        var first = new SourceSummary(1, 10, 1, 0, 0);
        var second = new SourceSummary(1, 20, 0, 1, 0);
        var third = new SourceSummary(1, 30, 0, 0, 1);

        Assert.Equal(
            first.Combine(second).Combine(third),
            first.Combine(second.Combine(third)));
    }
}
