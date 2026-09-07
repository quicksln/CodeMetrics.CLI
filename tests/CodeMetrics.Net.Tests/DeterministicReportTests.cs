using System.Globalization;
using CodeMetrics.Cli;
using CodeMetrics.Net.Tests.TestSupport;
using Xunit;

namespace CodeMetrics.Net.Tests;

/// <summary>
/// Identical source, options and tool version must produce byte-identical,
/// culture-invariant output. A report that changes shape on a German machine,
/// or between two runs, cannot be diffed or committed.
/// </summary>
public class DeterministicReportTests
{
    [Theory]
    [InlineData(OutputFormat.Table)]
    [InlineData(OutputFormat.Csv)]
    [InlineData(OutputFormat.Json)]
    [InlineData(OutputFormat.Html)]
    public void RepeatedRendering_ProducesIdenticalOutput(OutputFormat format)
    {
        var report = ReportFixture.Report(
            ReportFixture.Member("Alpha", cognitive: 9, maintainabilityIndex: 61.25),
            ReportFixture.Member("Beta", cognitive: 3, maintainabilityIndex: 88.5));

        var options = new Options { Path = "src", Format = format };

        Assert.Equal(ReportFixture.Render(report, options), ReportFixture.Render(report, options));
    }

    [Theory]
    // de-DE writes decimals with a comma, which would corrupt every csv row and
    // every json number if any formatting call used the ambient culture.
    [InlineData(OutputFormat.Csv, "70.5")]
    [InlineData(OutputFormat.Json, "70.5")]
    public void NumericFormatting_IgnoresTheAmbientCulture(OutputFormat format, string expected)
    {
        var output = InGermanCulture(() =>
            ReportFixture.Render(format, ReportFixture.Member(maintainabilityIndex: 70.5)));

        Assert.Contains(expected, output, StringComparison.Ordinal);
        Assert.DoesNotContain("70,5", output, StringComparison.Ordinal);
    }

    [Fact]
    public void TableAverages_IgnoreTheAmbientCulture()
    {
        var output = InGermanCulture(() => ReportFixture.Render(
            OutputFormat.Table,
            ReportFixture.Member("Alpha", cognitive: 1),
            ReportFixture.Member("Beta", cognitive: 2)));

        Assert.Contains("avg   1.5", output, StringComparison.Ordinal);
    }

    [Fact]
    public void CultureDoesNotChangeAnyReportByte()
    {
        var report = ReportFixture.Report(ReportFixture.Member(maintainabilityIndex: 70.5));
        var options = new Options { Path = "src", Format = OutputFormat.Csv };

        var invariant = ReportFixture.Render(report, options);
        var german = InGermanCulture(() => ReportFixture.Render(report, options));

        Assert.Equal(invariant, german);
    }

    [Fact]
    public void RepeatedAnalysis_ProducesEqualMetrics()
    {
        const string source = """
            class Widget
            {
                public int Score(int value)
                {
                    if (value > 0 && value < 10)
                    {
                        return value switch { 1 => 1, 2 => 2, _ => 0 };
                    }

                    return 0;
                }
            }
            """;

        // Every run writes a fresh temp file, so compare the measurements
        // rather than the paths they were measured from.
        Assert.Equal(Measurements(source), Measurements(source));
    }

    private static IReadOnlyList<string> Measurements(string source) =>
        AnalyzerFixture.Analyze(source)
            .Select(member => string.Create(
                CultureInfo.InvariantCulture,
                $"{member.FullName}:{member.LineNumber}:{member.CyclomaticComplexity}:{member.CognitiveComplexity}:{member.LinesOfCode}:{member.MaxNestingDepth}:{member.ParameterCount}:{member.MaintainabilityIndex:R}"))
            .ToList();

    /// <summary>
    /// Runs an action under a decimal-comma culture and restores the original,
    /// so a leaked culture can never affect another test.
    /// </summary>
    private static string InGermanCulture(Func<string> render)
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");

        try
        {
            return render();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
