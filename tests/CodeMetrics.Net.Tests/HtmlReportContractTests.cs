using CodeMetrics.Analysis;
using CodeMetrics.Cli;
using CodeMetrics.Reporting;
using Xunit;

namespace CodeMetrics.Net.Tests;

public class HtmlReportContractTests
{
    [Fact]
    public void WriteHtml_IncludesSeparateSourceSummaryPayload()
    {
        var report = new AnalysisReport(
            [
                new MemberMetrics
                {
                    FilePath = "sample.cs",
                    LineNumber = 10,
                    TypeName = "Example",
                    MemberName = "Run",
                    CyclomaticComplexity = 3,
                    CognitiveComplexity = 4,
                    LinesOfCode = 6,
                    MaxNestingDepth = 2,
                    ParameterCount = 0,
                    MaintainabilityIndex = 70
                }
            ],
            new SourceSummary(1, 42, 1, 1, 1));

        var buffer = new StringWriter();
        Reporters.Write(report, new Options { Path = "src", Format = OutputFormat.Html }, 1, buffer);

        var html = buffer.ToString();

        Assert.Contains("source-summary-data", html, StringComparison.Ordinal);
        Assert.Contains("\"fileCount\": 1", html, StringComparison.Ordinal);
        Assert.Contains("\"lineCount\": 42", html, StringComparison.Ordinal);
        Assert.Contains("\"classCount\": 1", html, StringComparison.Ordinal);
        Assert.Contains("\"recordCount\": 1", html, StringComparison.Ordinal);
        Assert.Contains("\"enumCount\": 1", html, StringComparison.Ordinal);
    }
}
