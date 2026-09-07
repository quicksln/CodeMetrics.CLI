using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace CodeMetrics.Net.Tests.TestSupport;

/// <summary>
/// Parses a member snippet the way <c>MetricsAnalyzer.Measure</c> does: the
/// walkers see the body, never the declaration. Both complexity metrics go
/// through this one path so a snippet that scores N cyclomatic and M cognitive
/// is measured over the identical node.
/// </summary>
internal static class SyntaxFixture
{
    private static readonly CSharpParseOptions ParseOptions =
        new(LanguageVersion.Preview, DocumentationMode.None);

    /// <summary>
    /// Parameters cover every identifier the body snippets need, so the
    /// snippets stay readable and still parse without errors.
    /// </summary>
    public const string Signature =
        "void M(int x, object? o, string? s, bool a, bool b, bool c, bool d)";

    /// <summary>The name the signature declares, for the recursion rule.</summary>
    public const string MemberName = "M";

    public static SyntaxNode Body(string statements) => MemberBody($"{Signature} {{ {statements} }}");

    public static SyntaxNode MemberBody(string memberSource)
    {
        var tree = CSharpSyntaxTree.ParseText($"class C {{ {memberSource} }}", ParseOptions);

        var errors = tree.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.ToString())
            .ToList();

        Assert.True(errors.Count == 0, string.Join("; ", errors));

        var method = tree.GetRoot().DescendantNodes().OfType<BaseMethodDeclarationSyntax>().Single();
        var body = (SyntaxNode?)method.Body ?? method.ExpressionBody;

        Assert.NotNull(body);

        return body;
    }
}
