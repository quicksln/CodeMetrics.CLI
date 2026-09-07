using CodeMetrics.Analysis;
using Xunit;

namespace CodeMetrics.Net.Tests;

/// <summary>
/// samples/ComplexitySamples.cs documents the score of every method in its own
/// header comment. Those numbers are the tool's worked examples, so they are a
/// contract: this test makes the comment fail the build when it stops being true.
/// </summary>
public class ComplexitySamplesGoldenTests
{
    private static readonly Lazy<IReadOnlyList<MemberMetrics>> Members = new(AnalyzeSamples);

    [Theory]
    // Nested loops with a jump out of the inner one.
    [InlineData("SumOfPrimes", 7, 4)]
    // Wide but flat: four paths to test, one thing to read.
    [InlineData("Describe", 1, 4)]
    // One run of &&, then one of ||.
    [InlineData("IsEligible", 3, 5)]
    // An else-if chain costs 1 per link and never nests.
    [InlineData("Grade", 4, 4)]
    // Pattern combinators are decision points but still read as one switch.
    [InlineData("Classify", 1, 6)]
    // Null shorthand: three paths to test, nothing to read.
    [InlineData("Label", 0, 4)]
    // foreach, if, nested if and a catch, each one level deeper.
    [InlineData("Process", 9, 5)]
    public void Sample_ScoresTheValueItsCommentClaims(string memberName, int cognitive, int cyclomatic)
    {
        var member = Assert.Single(Members.Value, candidate => candidate.MemberName == memberName);

        Assert.Equal(cognitive, member.CognitiveComplexity);
        Assert.Equal(cyclomatic, member.CyclomaticComplexity);
    }

    [Fact]
    public void EverySampleMemberIsCoveredByTheGoldenTable()
    {
        // A sample added without an entry above would otherwise go unverified.
        Assert.Equal(7, Members.Value.Count);
    }

    [Fact]
    public void Samples_ReportTheContainingType()
    {
        Assert.All(Members.Value, member => Assert.Equal("ComplexitySamples", member.TypeName));
    }

    private static IReadOnlyList<MemberMetrics> AnalyzeSamples()
    {
        // Copied next to the test assembly by the csproj, so the test does not
        // depend on the working directory or the repository layout.
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ComplexitySamples.cs");

        Assert.True(File.Exists(path), $"Sample fixture not found at {path}.");

        var result = MetricsAnalyzer.AnalyzeFile(path);

        Assert.NotNull(result.SourceSummary);

        return result.Members;
    }
}
