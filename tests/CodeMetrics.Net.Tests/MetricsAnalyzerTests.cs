using CodeMetrics.Analysis;
using CodeMetrics.Net.Tests.TestSupport;
using Xunit;

namespace CodeMetrics.Net.Tests;

/// <summary>
/// Member selection: which declarations become a reported row. The set is a
/// public contract, so each supported kind gets a test.
/// </summary>
public class MetricsAnalyzerTests
{
    [Fact]
    public void AnalyzeFile_ReportsTopLevelStatementsAsOneMember()
    {
        const string source = """
            using System;

            var count = 0;

            if (args.Length > 0)
            {
                count++;
            }

            foreach (var arg in args)
            {
                Console.WriteLine(arg);
            }

            return count;
            """;

        var member = Assert.Single(Analyze(source));

        Assert.Equal("<top-level>", member.TypeName);
        Assert.Equal("<main>", member.MemberName);

        // The whole program is one member: base 1, plus the if and the foreach.
        Assert.Equal(3, member.CyclomaticComplexity);

        // The first statement, not the synthetic block wrapped around it.
        Assert.Equal(3, member.LineNumber);

        // Top-level statements declare no parameter list; "args" is implicit.
        Assert.Equal(0, member.ParameterCount);
    }

    [Fact]
    public void AnalyzeFile_KeepsTopLevelStatementsSeparateFromLaterTypes()
    {
        const string source = """
            var ready = true;

            if (ready)
            {
                System.Console.WriteLine("go");
            }

            class Helper
            {
                public int Score(int value)
                {
                    return value > 0 ? 1 : 0;
                }
            }
            """;

        var members = Analyze(source);

        Assert.Equal(2, members.Count);

        var topLevel = Assert.Single(members, member => member.MemberName == "<main>");
        Assert.Equal(2, topLevel.CyclomaticComplexity);

        var score = Assert.Single(members, member => member.MemberName == "Score");
        Assert.Equal("Helper", score.TypeName);

        // The method body must be counted once, in its own row only.
        Assert.Equal(2, score.CyclomaticComplexity);
    }

    [Fact]
    public void AnalyzeFile_AddsNoTopLevelMemberWhenFileHasOnlyTypes()
    {
        const string source = """
            namespace Example;

            class Widget
            {
                public int Size { get; set; }

                public int Clamp(int value) => value < 0 ? 0 : value;
            }
            """;

        var member = Assert.Single(Analyze(source));

        Assert.Equal("Clamp", member.MemberName);
        Assert.Equal(2, member.CyclomaticComplexity);
    }

    private static IReadOnlyList<MemberMetrics> Analyze(string source) => AnalyzerFixture.Analyze(source);
}
