using CodeMetrics.Analysis;
using Xunit;

namespace CodeMetrics.Net.Tests;

public class SourceSummaryTests
{
    [Fact]
    public void EmptySummary_HasZeroedCounts()
    {
        var summary = SourceSummary.Empty;

        Assert.Equal(0, summary.FileCount);
        Assert.Equal(0, summary.LineCount);
        Assert.Equal(0, summary.ClassCount);
        Assert.Equal(0, summary.RecordCount);
        Assert.Equal(0, summary.EnumCount);
    }

    [Fact]
    public void Summary_CombinesSuccessfulFileTotals()
    {
        var first = new SourceSummary(2, 24, 3, 2, 1);
        var second = new SourceSummary(3, 18, 1, 4, 2);

        var combined = first.Combine(second);

        Assert.Equal(5, combined.FileCount);
        Assert.Equal(42, combined.LineCount);
        Assert.Equal(4, combined.ClassCount);
        Assert.Equal(6, combined.RecordCount);
        Assert.Equal(3, combined.EnumCount);
    }
}
