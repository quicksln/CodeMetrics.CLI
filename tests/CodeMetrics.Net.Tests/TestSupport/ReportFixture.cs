using CodeMetrics.Analysis;
using CodeMetrics.Cli;
using CodeMetrics.Reporting;

namespace CodeMetrics.Net.Tests.TestSupport;

/// <summary>
/// Builds reports for the output-format contract tests. Every reporter writes
/// to a <see cref="TextWriter"/>, so nothing here touches the file system.
/// </summary>
internal static class ReportFixture
{
    public static MemberMetrics Member(
        string memberName = "Run",
        string typeName = "Example",
        string filePath = "sample.cs",
        int lineNumber = 10,
        int cognitive = 4,
        int cyclomatic = 3,
        int linesOfCode = 6,
        int maxNestingDepth = 2,
        int parameterCount = 0,
        double maintainabilityIndex = 70d) =>
        new()
        {
            FilePath = filePath,
            LineNumber = lineNumber,
            TypeName = typeName,
            MemberName = memberName,
            CyclomaticComplexity = cyclomatic,
            CognitiveComplexity = cognitive,
            LinesOfCode = linesOfCode,
            MaxNestingDepth = maxNestingDepth,
            ParameterCount = parameterCount,
            MaintainabilityIndex = maintainabilityIndex
        };

    public static AnalysisReport Report(params MemberMetrics[] members) =>
        new(members, new SourceSummary(1, 100, 1, 0, 0));

    public static string Render(AnalysisReport report, Options options)
    {
        var buffer = new StringWriter();
        Reporters.Write(report, options, buffer);

        return buffer.ToString();
    }

    public static string Render(OutputFormat format, params MemberMetrics[] members) =>
        Render(Report(members), new Options { Path = "src", Format = format });

    /// <summary>Output lines with the trailing blank line removed.</summary>
    public static string[] Lines(string output) =>
        output.Replace("\r\n", "\n", StringComparison.Ordinal)
            .TrimEnd('\n')
            .Split('\n');
}
