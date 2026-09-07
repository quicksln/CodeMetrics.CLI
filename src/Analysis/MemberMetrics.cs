namespace CodeMetrics.Analysis;

/// <summary>
/// Metrics for one analysable member: a method, constructor, operator,
/// property accessor or expression-bodied member.
/// </summary>
public sealed record MemberMetrics
{
    public required string FilePath { get; init; }

    /// <summary>1-based line of the declaration, so editors can jump straight to it.</summary>
    public required int LineNumber { get; init; }

    public required string TypeName { get; init; }

    public required string MemberName { get; init; }

    public required int CyclomaticComplexity { get; init; }

    public required int CognitiveComplexity { get; init; }

    public required int LinesOfCode { get; init; }

    public required int MaxNestingDepth { get; init; }

    public required int ParameterCount { get; init; }

    /// <summary>Visual Studio style maintainability index, 0 (bad) to 100 (good).</summary>
    public required double MaintainabilityIndex { get; init; }

    public string FullName => $"{TypeName}.{MemberName}";
}
