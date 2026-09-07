namespace CodeMetrics.Analysis;

/// <summary>
/// Aggregate whole-file source inventory for one command execution.
/// </summary>
public sealed record SourceSummary(
    int FileCount,
    int LineCount,
    int ClassCount,
    int RecordCount,
    int EnumCount)
{
    public static SourceSummary Empty { get; } = new(0, 0, 0, 0, 0);

    public SourceSummary Combine(SourceSummary other) =>
        new(
            FileCount + other.FileCount,
            LineCount + other.LineCount,
            ClassCount + other.ClassCount,
            RecordCount + other.RecordCount,
            EnumCount + other.EnumCount);
}
