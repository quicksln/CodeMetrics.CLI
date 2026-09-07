namespace CodeMetrics.Analysis;

/// <summary>
/// Ordered member metrics plus the aggregate source summary for a complete run.
/// </summary>
public sealed record AnalysisReport(
    IReadOnlyList<MemberMetrics> Members,
    SourceSummary SourceSummary);
