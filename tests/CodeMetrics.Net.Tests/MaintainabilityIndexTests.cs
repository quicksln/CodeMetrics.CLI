using CodeMetrics.Net.Tests.TestSupport;
using Xunit;

namespace CodeMetrics.Net.Tests;

/// <summary>
/// The maintainability index is published in every report, so its scale is a
/// contract even though the calculator itself is internal. These tests reach it
/// through the analyzer, the same way a user does.
/// </summary>
public class MaintainabilityIndexTests
{
    [Fact]
    public void EmptyMember_ScoresTheFormulaCeiling()
    {
        // An empty body has no Halstead vocabulary and one line, so both
        // logarithm terms vanish and only the cyclomatic term is left:
        // (171 - 0.23 * 1) * 100 / 171 = 99.87. Pinning this one number pins
        // the volume floor, the line floor, the 0.23 weight and the rebase.
        var member = Assert.Single(AnalyzerFixture.Analyze("class C { void Run() { } }"));

        Assert.Equal(99.87, member.MaintainabilityIndex, 2);
    }

    [Fact]
    public void Score_StaysWithinTheDocumentedRange()
    {
        const string source = """
            using System;
            using System.Collections.Generic;

            class Widget
            {
                public int Trivial() => 1;

                public int Tangled(IEnumerable<string> items, int limit)
                {
                    var total = 0;

                    foreach (var item in items)
                    {
                        if (item.Length > limit)
                        {
                            for (var index = 0; index < item.Length; index++)
                            {
                                switch (item[index])
                                {
                                    case 'a':
                                        total += 1;
                                        break;
                                    case 'b':
                                        total += 2;
                                        break;
                                    default:
                                        total -= 1;
                                        break;
                                }
                            }
                        }
                        else if (item.Length == limit)
                        {
                            total = total > 0 ? total - 1 : 0;
                        }
                    }

                    return total;
                }
            }
            """;

        var members = AnalyzerFixture.Analyze(source);

        Assert.All(members, member =>
        {
            Assert.InRange(member.MaintainabilityIndex, 0d, 100d);
        });
    }

    [Fact]
    public void LongerAndMoreComplexMembers_ScoreLower()
    {
        // The absolute value is an approximation; the ranking is what the
        // report is read for.
        const string source = """
            class Widget
            {
                public int Trivial() => 1;

                public int Tangled(int value)
                {
                    var total = 0;

                    for (var index = 0; index < value; index++)
                    {
                        if (index % 2 == 0 && index > 10)
                        {
                            total += index * 3;
                        }
                        else
                        {
                            total -= index;
                        }
                    }

                    return total;
                }
            }
            """;

        var trivial = AnalyzerFixture.Member(source, "Trivial");
        var tangled = AnalyzerFixture.Member(source, "Tangled");

        Assert.True(
            tangled.MaintainabilityIndex < trivial.MaintainabilityIndex,
            $"tangled {tangled.MaintainabilityIndex} should score below trivial {trivial.MaintainabilityIndex}");
    }

    [Fact]
    public void Score_IgnoresFormattingThatCarriesNoCode()
    {
        // Halstead measures tokens, and lines of code skips blanks and
        // comments, so reformatting must not move the score.
        const string dense = """
            class C
            {
                public int Run(int value)
                {
                    return value > 0 ? value : 0;
                }
            }
            """;

        const string spaced = """
            class C
            {
                // Explains the guard.
                public int Run(int value)
                {

                    // Negative input clamps to zero.

                    return value > 0 ? value : 0;

                }
            }
            """;

        var denseMember = Assert.Single(AnalyzerFixture.Analyze(dense));
        var spacedMember = Assert.Single(AnalyzerFixture.Analyze(spaced));

        Assert.Equal(denseMember.MaintainabilityIndex, spacedMember.MaintainabilityIndex, 6);
    }
}
