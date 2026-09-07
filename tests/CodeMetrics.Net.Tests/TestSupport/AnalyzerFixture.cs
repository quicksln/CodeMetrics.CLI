using CodeMetrics.Analysis;

namespace CodeMetrics.Net.Tests.TestSupport;

/// <summary>
/// <c>MetricsAnalyzer.AnalyzeFile</c> takes a path, not text, so every
/// end-to-end measurement goes through a real file on disk.
/// </summary>
internal static class AnalyzerFixture
{
    public static IReadOnlyList<MemberMetrics> Analyze(string source) => AnalyzeResult(source).Members;

    public static FileAnalysisResult AnalyzeResult(string source)
    {
        var path = Path.Combine(Path.GetTempPath(), $"codemetrics-{Guid.NewGuid():N}.cs");
        File.WriteAllText(path, source);

        try
        {
            return MetricsAnalyzer.AnalyzeFile(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Finds the single member with the given name, or fails.</summary>
    public static MemberMetrics Member(string source, string memberName) =>
        Analyze(source).Single(member => member.MemberName == memberName);
}
