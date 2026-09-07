using CodeMetrics.Analysis;
using CodeMetrics.Net.Tests.TestSupport;
using Microsoft.CodeAnalysis;
using Xunit;

namespace CodeMetrics.Net.Tests;

/// <summary>
/// One case per construct the walker recognises, plus equivalence pairs that
/// pin the rule the metric exists to express: two spellings of the same logic
/// must score the same.
/// </summary>
public class CyclomaticComplexityWalkerTests
{
    [Fact]
    public void EmptyBody_ScoresOne()
    {
        Assert.Equal(1, Body(string.Empty));
    }

    [Theory]
    // Branching statements.
    [InlineData("if (a) { }", 2)]
    [InlineData("if (a) { } else { }", 2)]
    [InlineData("if (a) { } else if (b) { } else { }", 3)]
    [InlineData("while (a) { }", 2)]
    [InlineData("do { } while (a);", 2)]
    [InlineData("for (var i = 0; i < x; i++) { }", 2)]
    [InlineData("foreach (var item in s) { }", 2)]
    [InlineData("foreach (var (p, q) in o) { }", 2)]
    // Switches: one increment per case, none for the fall-through.
    [InlineData("switch (x) { case 1: break; case 2: break; default: break; }", 3)]
    [InlineData("_ = x switch { 1 => \"a\", 2 => \"b\", _ => \"c\" };", 3)]
    [InlineData("switch (x) { case 1 when a: break; }", 3)]
    // Exception handling: catch and its filter each decide a path, finally does not.
    [InlineData("try { } catch (System.Exception) { }", 2)]
    [InlineData("try { } catch (System.Exception) when (a) { }", 3)]
    [InlineData("try { } finally { }", 1)]
    // Conditional expressions and null shorthand.
    [InlineData("_ = a ? 1 : 2;", 2)]
    [InlineData("_ = s?.Length;", 2)]
    [InlineData("_ = s?.Trim()?.Length;", 3)]
    [InlineData("_ = s ?? \"x\";", 2)]
    [InlineData("s ??= \"x\";", 2)]
    // Each boolean operator is its own predicate, per McCabe.
    [InlineData("if (a && b) { }", 3)]
    [InlineData("if (a && b && c || d) { }", 5)]
    // Nested callables fold into the containing member.
    [InlineData("System.Func<int, int> f = n => n > 0 ? 1 : 0;", 2)]
    [InlineData("void Local() { if (a) { } } Local();", 2)]
    // An unconditional jump adds an edge but no decision.
    [InlineData("goto end; end: ;", 1)]
    public void Body_ScoresExpectedComplexity(string body, int expected)
    {
        Assert.Equal(expected, Body(body));
    }

    [Fact]
    public void ExpressionBodiedMember_ScoresOne()
    {
        Assert.Equal(1, Member("int P(int x) => x;"));
    }

    [Theory]
    // "or" and "and" are compound predicates, exactly like "||" and "&&".
    [InlineData("if (x is 1 or 2) { }", 3)]
    [InlineData("if (x is 1 or 2 or 3) { }", 4)]
    [InlineData("if (x is > 0 and < 10) { }", 3)]
    [InlineData("switch (x) { case 1 or 2: break; }", 3)]
    // "not" negates a single test, so it adds no path.
    [InlineData("if (x is not 0) { }", 2)]
    // A bare discard is the fall-through, whichever switch form it appears in.
    [InlineData("switch (x) { case _: break; }", 1)]
    [InlineData("switch (x) { case _ when a: break; }", 2)]
    // Only a bare "_" reads as the discard: real constants still branch.
    [InlineData("switch (o) { case null: break; }", 2)]
    [InlineData("switch (x) { case @_: break; }", 2)]
    public void PatternMatching_ScoresExpectedComplexity(string body, int expected)
    {
        Assert.Equal(expected, Body(body));
    }

    [Theory]
    [InlineData("if (x is 1 or 2) { }", "if (x == 1 || x == 2) { }")]
    [InlineData("if (x is 1 or 2 or 3) { }", "if (x == 1 || x == 2 || x == 3) { }")]
    [InlineData("if (x is > 0 and < 10) { }", "if (x > 0 && x < 10) { }")]
    [InlineData("switch (x) { case 1 or 2: break; }", "switch (x) { case 1: case 2: break; }")]
    [InlineData("switch (x) { case _: break; }", "switch (x) { default: break; }")]
    public void EquivalentSpellings_ScoreTheSame(string pattern, string equivalent)
    {
        Assert.Equal(Body(equivalent), Body(pattern));
    }

    private static int Body(string statements) => Measure(SyntaxFixture.Body(statements));

    /// <summary>
    /// Measures a member the way <c>MetricsAnalyzer.Measure</c> does: the walker
    /// sees the body, never the declaration.
    /// </summary>
    private static int Member(string memberSource) => Measure(SyntaxFixture.MemberBody(memberSource));

    private static int Measure(SyntaxNode body)
    {
        var walker = new CyclomaticComplexityWalker();
        walker.Visit(body);

        return walker.Complexity;
    }
}
