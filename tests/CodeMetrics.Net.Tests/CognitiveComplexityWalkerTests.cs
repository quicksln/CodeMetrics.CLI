using CodeMetrics.Analysis;
using CodeMetrics.Net.Tests.TestSupport;
using Microsoft.CodeAnalysis;
using Xunit;

namespace CodeMetrics.Net.Tests;

/// <summary>
/// Cognitive complexity measures how hard a member is to read, not how many
/// paths it has. The cases below are chosen around the three SonarSource rules
/// and, deliberately, around the places where this metric disagrees with
/// cyclomatic complexity: a wide switch is easy to read, and null shorthand
/// costs nothing even though every <c>?.</c> is a path to test.
/// </summary>
public class CognitiveComplexityWalkerTests
{
    [Fact]
    public void EmptyBody_ScoresZero()
    {
        // Unlike cyclomatic complexity there is no base of 1: straight-line
        // code costs nothing to read.
        Assert.Equal(0, Body(string.Empty));
    }

    [Theory]
    // Rule 1: structures that break linear reading flow score +1.
    [InlineData("if (a) { }", 1)]
    [InlineData("while (a) { }", 1)]
    [InlineData("do { } while (a);", 1)]
    [InlineData("for (var i = 0; i < x; i++) { }", 1)]
    [InlineData("foreach (var item in s) { }", 1)]
    [InlineData("foreach (var (p, q) in o) { }", 1)]
    [InlineData("try { } catch (System.Exception) { }", 1)]
    [InlineData("_ = a ? 1 : 2;", 1)]
    // A jump to a label breaks the flow outright.
    [InlineData("goto end; end: ;", 1)]
    // A whole switch is one increment however wide: comparing one value against
    // a list of literals is one thing to read. Cyclomatic complexity scores 3.
    [InlineData("switch (x) { case 1: break; case 2: break; default: break; }", 1)]
    [InlineData("_ = x switch { 1 => \"a\", 2 => \"b\", _ => \"c\" };", 1)]
    // Rule 3: shorthand that condenses lines is free.
    [InlineData("_ = s?.Length;", 0)]
    [InlineData("_ = s?.Trim()?.Length;", 0)]
    [InlineData("_ = s ?? \"x\";", 0)]
    [InlineData("s ??= \"x\";", 0)]
    // try and finally introduce no decision to follow; only catch does.
    [InlineData("try { } finally { }", 0)]
    // else and else-if are flat: +1 each, no nesting penalty.
    [InlineData("if (a) { } else { }", 2)]
    [InlineData("if (a) { } else if (b) { }", 2)]
    [InlineData("if (a) { } else if (b) { } else { }", 3)]
    // Rule 2: nesting adds +1 per level.
    [InlineData("if (a) { if (b) { } }", 3)]
    [InlineData("if (a) { if (b) { if (c) { } } }", 6)]
    [InlineData("while (a) { if (b) { } }", 3)]
    [InlineData("foreach (var item in s) { if (a) { } }", 3)]
    [InlineData("if (a) { switch (x) { case 1: break; } }", 3)]
    [InlineData("try { } catch (System.Exception) { if (a) { } }", 3)]
    // Siblings are not nested, so they cost the same as each other.
    [InlineData("if (a) { } if (b) { }", 2)]
    // A run of the same boolean operator costs 1 however long it is; mixing
    // operators costs 1 per run. Cyclomatic complexity counts every operator.
    [InlineData("if (a && b) { }", 2)]
    [InlineData("if (a && b && c) { }", 2)]
    [InlineData("if (a || b || c) { }", 2)]
    [InlineData("if (a && b && c || d) { }", 3)]
    // Parentheses start a fresh run, because they are read as their own clause.
    [InlineData("if ((a || b) && c) { }", 3)]
    // The operators cost their run even without a surrounding structure.
    [InlineData("_ = a && b;", 1)]
    // Declaring a callable is free, but its body reads as one level deeper.
    [InlineData("System.Func<int, int> f = n => n > 0 ? 1 : 0;", 2)]
    [InlineData("System.Func<int, int> f = (n) => n > 0 ? 1 : 0;", 2)]
    [InlineData("System.Func<int, int> f = delegate (int n) { if (a) { return 1; } return 0; };", 2)]
    [InlineData("void Local() { if (a) { } } Local();", 2)]
    // Recursion is a fundamental increment: flat +1, no nesting penalty.
    [InlineData("M();", 1)]
    [InlineData("this.M();", 1)]
    [InlineData("if (a) { M(); }", 2)]
    // Calling anything else is just a call.
    [InlineData("System.Console.WriteLine();", 0)]
    [InlineData("Other();", 0)]
    public void Body_ScoresExpectedComplexity(string body, int expected)
    {
        Assert.Equal(expected, Body(body));
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("if (a) { }", 1)]
    [InlineData("if (a) { if (b) { } }", 2)]
    [InlineData("if (a) { if (b) { if (c) { } } }", 3)]
    // Siblings never deepen the tree.
    [InlineData("if (a) { } if (b) { }", 1)]
    // A switch nests its sections, a ternary nests its branches.
    [InlineData("switch (x) { case 1: break; }", 1)]
    [InlineData("_ = a ? 1 : 2;", 1)]
    // A lambda or catch body counts as a level even though it costs nothing.
    [InlineData("System.Func<int, int> f = n => n;", 1)]
    [InlineData("try { } catch (System.Exception) { }", 1)]
    [InlineData("try { } finally { }", 0)]
    public void MaxNestingDepth_TracksDeepestStructure(string body, int expected)
    {
        Assert.Equal(expected, Nesting(body));
    }

    [Fact]
    public void FlatChain_ScoresLowerThanEquivalentNesting()
    {
        // The rule the metric exists to express: three decisions written as a
        // chain read more easily than the same three decisions nested, even
        // though cyclomatic complexity cannot tell them apart.
        var chained = Body("if (a) { } else if (b) { } else if (c) { } else { }");
        var nested = Body("if (a) { if (b) { if (c) { } } }");

        Assert.Equal(4, chained);
        Assert.Equal(6, nested);
        Assert.True(chained < nested, $"chained {chained} should read more easily than nested {nested}");
    }

    [Fact]
    public void WideSwitch_ScoresTheSameAsNarrowSwitch()
    {
        // Adding cases to a switch adds paths but nothing to read.
        var narrow = Body("switch (x) { case 1: break; }");
        var wide = Body("switch (x) { case 1: break; case 2: break; case 3: break; case 4: break; }");

        Assert.Equal(narrow, wide);
    }

    private static int Body(string statements) => Measure(SyntaxFixture.Body(statements)).Complexity;

    private static int Nesting(string statements) => Measure(SyntaxFixture.Body(statements)).MaxNestingDepth;

    private static CognitiveComplexityWalker Measure(SyntaxNode body)
    {
        var walker = new CognitiveComplexityWalker(SyntaxFixture.MemberName);
        walker.Visit(body);

        return walker;
    }
}
