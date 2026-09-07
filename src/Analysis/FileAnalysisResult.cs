namespace CodeMetrics.Analysis;

/// <summary>
/// Result of analyzing a single discovered C# source file.
/// </summary>
public sealed record FileAnalysisResult(
    string FilePath,
    IReadOnlyList<MemberMetrics> Members,
    SourceSummary? SourceSummary)
{
    public static FileAnalysisResult Skipped(string filePath) =>
        new(filePath, [], null);

    public static FileAnalysisResult Successful(string filePath, IReadOnlyList<MemberMetrics> members, SourceSummary summary) =>
        new(filePath, members, summary);
}
