using CodeMetrics.Analysis;
using CodeMetrics.Net.Tests.TestSupport;
using Xunit;

namespace CodeMetrics.Net.Tests;

/// <summary>
/// Which declarations become a reported row, and what they are called, is a
/// documented contract: adding or dropping a kind silently changes every
/// member count, average and gate result a user has recorded.
/// </summary>
public class MetricsAnalyzerMemberKindsTests
{
    [Theory]
    [InlineData("void Run() { }", "Run")]
    [InlineData("public C() { }", "C.ctor")]
    [InlineData("~C() { }", "~C")]
    [InlineData("public static C operator +(C left, C right) { return left; }", "operator +")]
    [InlineData("public static explicit operator int(C value) { return 0; }", "operator int")]
    // Expression bodies are measured just like block bodies.
    [InlineData("int Run() => 1;", "Run")]
    [InlineData("public int Size => 1;", "Size")]
    [InlineData("public int this[int index] => index;", "this[]")]
    public void ReportedMember_IsNamedAsDocumented(string declaration, string expectedName)
    {
        var member = Assert.Single(Analyze($"class C {{ {declaration} }}"));

        Assert.Equal(expectedName, member.MemberName);
        Assert.Equal("C", member.TypeName);
    }

    [Theory]
    // Accessors are reported individually, qualified by what they belong to.
    [InlineData("public int Size { get { return 1; } }", "Size.get")]
    [InlineData("public int Size { set { } }", "Size.set")]
    [InlineData("public int Size { init { } }", "Size.init")]
    [InlineData("public event System.EventHandler? E { add { } remove { } }", "E.add")]
    [InlineData("public int this[int index] { get { return index; } }", "this[].get")]
    public void Accessor_IsNamedAfterItsOwner(string declaration, string expectedName)
    {
        var members = Analyze($"class C {{ {declaration} }}");

        Assert.Contains(members, member => member.MemberName == expectedName);
    }

    [Theory]
    // Nothing to measure means nothing to report.
    [InlineData("abstract class C { public abstract void Run(); }")]
    [InlineData("interface IC { void Run(); }")]
    [InlineData("class C { public int Size { get; set; } }")]
    [InlineData("partial class C { partial void Run(); }")]
    [InlineData("class C { public extern void Run(); }")]
    // A declaration with no members at all.
    [InlineData("enum E { A, B }")]
    public void BodilessDeclaration_IsNotReported(string source)
    {
        Assert.Empty(Analyze(source));
    }

    [Fact]
    public void NestedType_IsReportedWithItsOuterChain()
    {
        const string source = """
            namespace Example;

            class Outer
            {
                class Middle
                {
                    struct Inner
                    {
                        public int Run() => 1;
                    }
                }
            }
            """;

        var member = Assert.Single(Analyze(source));

        // Namespaces are not part of the chain; containing types are.
        Assert.Equal("Outer.Middle.Inner", member.TypeName);
        Assert.Equal("Outer.Middle.Inner.Run", member.FullName);
    }

    [Fact]
    public void LineNumber_IsTheOneBasedLineOfTheDeclaration()
    {
        const string source = """
            class Widget
            {
                public int Run()
                {
                    return 1;
                }
            }
            """;

        var member = Assert.Single(Analyze(source));

        Assert.Equal(3, member.LineNumber);
    }

    [Theory]
    [InlineData("void Run() { }", 0)]
    [InlineData("void Run(int a) { }", 1)]
    [InlineData("void Run(int a, string b, bool c) { }", 3)]
    // An indexer counts its index parameters; an accessor takes none.
    [InlineData("public int this[int a, int b] => a;", 2)]
    public void ParameterCount_ComesFromTheParameterList(string declaration, int expected)
    {
        var member = Assert.Single(Analyze($"class C {{ {declaration} }}"));

        Assert.Equal(expected, member.ParameterCount);
    }

    [Fact]
    public void LinesOfCode_IgnoresBlankLinesCommentsAndBraces()
    {
        const string source = """
            class Widget
            {
                public int Run()
                {
                    // A comment does not count.
                    var value = 1;

                    /* Nor does a block comment,
                     * including its continuation. */
                    return value;
                }
            }
            """;

        var member = Assert.Single(Analyze(source));

        // Only "var value = 1;" and "return value;" carry code.
        Assert.Equal(2, member.LinesOfCode);
    }

    [Fact]
    public void LinesOfCode_IsNeverZero()
    {
        // An empty member still occupies a line in the report.
        var member = Assert.Single(Analyze("class C { void Run() { } }"));

        Assert.Equal(1, member.LinesOfCode);
    }

    [Fact]
    public void MembersAreReportedInDocumentOrder()
    {
        const string source = """
            class Widget
            {
                public int First() => 1;

                public int Second() => 2;

                public int Third() => 3;
            }
            """;

        Assert.Equal(
            ["First", "Second", "Third"],
            Analyze(source).Select(member => member.MemberName));
    }

    [Fact]
    public void EveryMemberRecordsTheFileItCameFrom()
    {
        var result = AnalyzerFixture.AnalyzeResult("class C { void Run() { } }");

        Assert.All(result.Members, member => Assert.Equal(result.FilePath, member.FilePath));
    }

    private static IReadOnlyList<MemberMetrics> Analyze(string source) => AnalyzerFixture.Analyze(source);
}
