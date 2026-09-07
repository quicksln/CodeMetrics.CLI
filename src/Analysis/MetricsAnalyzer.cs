using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CodeMetrics.Analysis;

/// <summary>
/// Parses a file into a syntax tree and measures every member in it.
/// </summary>
public static class MetricsAnalyzer
{
    private static readonly CSharpParseOptions ParseOptions =
        new(LanguageVersion.Preview, DocumentationMode.None);

    public static FileAnalysisResult AnalyzeFile(string filePath)
    {
        try
        {
            var text = File.ReadAllText(filePath);
            var tree = CSharpSyntaxTree.ParseText(text, ParseOptions, filePath);
            var root = tree.GetCompilationUnitRoot();
            var summary = CreateSourceSummary(root, text);
            var members = EnumerateMembers(root)
                .Select(candidate => Measure(filePath, candidate))
                .ToList();

            return FileAnalysisResult.Successful(filePath, members, summary);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"warning: skipped {filePath} ({ex.Message})");
            return FileAnalysisResult.Skipped(filePath);
        }
    }

    private static MemberMetrics Measure(string filePath, MemberCandidate candidate)
    {
        var cyclomatic = new CyclomaticComplexityWalker();
        cyclomatic.Visit(candidate.Body);

        var cognitive = new CognitiveComplexityWalker(candidate.Name);
        cognitive.Visit(candidate.Body);

        var linesOfCode = CountLinesOfCode(candidate.Body);
        var volume = HalsteadCalculator.CalculateVolume(candidate.Body);
        var line = candidate.Declaration.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

        return new MemberMetrics
        {
            FilePath = filePath,
            LineNumber = line,
            TypeName = DescribeContainingType(candidate.Declaration),
            MemberName = candidate.Name,
            CyclomaticComplexity = cyclomatic.Complexity,
            CognitiveComplexity = cognitive.Complexity,
            LinesOfCode = linesOfCode,
            MaxNestingDepth = cognitive.MaxNestingDepth,
            ParameterCount = candidate.ParameterCount,
            MaintainabilityIndex = HalsteadCalculator.CalculateMaintainabilityIndex(
                volume,
                cyclomatic.Complexity,
                linesOfCode)
        };
    }

    private static IEnumerable<MemberCandidate> EnumerateMembers(CompilationUnitSyntax root)
    {
        // Top-level statements always precede any type declaration, so yielding
        // them first keeps members in document order.
        if (CreateTopLevelCandidate(root) is { } topLevel)
        {
            yield return topLevel;
        }

        foreach (var node in root.DescendantNodes())
        {
            if (CreateDeclaredCandidate(node) is { } declared)
            {
                yield return declared;
            }
        }
    }

    /// <summary>
    /// The member kinds that carry metrics. This set is a public contract, so
    /// adding to it is a documented behaviour change.
    /// </summary>
    private static MemberCandidate? CreateDeclaredCandidate(SyntaxNode node) => node switch
    {
        // Methods, constructors, destructors, operators.
        BaseMethodDeclarationSyntax method => Candidate(
            method,
            (SyntaxNode?)method.Body ?? method.ExpressionBody,
            DescribeMethod(method),
            method.ParameterList.Parameters.Count),

        // get / set / init / add / remove blocks.
        AccessorDeclarationSyntax accessor => Candidate(
            accessor,
            (SyntaxNode?)accessor.Body ?? accessor.ExpressionBody,
            DescribeAccessor(accessor),
            0),

        // Expression-bodied properties and indexers have no accessor node.
        PropertyDeclarationSyntax { ExpressionBody: { } arrow } property =>
            Candidate(property, arrow, property.Identifier.Text, 0),

        IndexerDeclarationSyntax { ExpressionBody: { } arrow } indexer =>
            Candidate(indexer, arrow, "this[]", indexer.ParameterList.Parameters.Count),

        _ => null
    };

    /// <summary>
    /// A declaration with no body has nothing to measure, so it is not reported.
    /// Abstract and interface members and auto-property accessors land here.
    /// </summary>
    private static MemberCandidate? Candidate(SyntaxNode declaration, SyntaxNode? body, string name, int parameterCount) =>
        body is null ? null : new MemberCandidate(declaration, body, name, parameterCount);

    /// <summary>
    /// A top-level statement program has no declaration to hang metrics on, so
    /// the whole program becomes one synthetic member named <c>&lt;main&gt;</c>.
    /// Nothing else in the file is affected: types declared after the
    /// statements are still found by the caller's own walk.
    /// </summary>
    private static MemberCandidate? CreateTopLevelCandidate(CompilationUnitSyntax unit)
    {
        var statements = unit.Members.OfType<GlobalStatementSyntax>().ToList();

        if (statements.Count == 0)
        {
            return null;
        }

        // The metric walkers each take a single body node, so the statements are
        // rehosted in a generated block. Only the walkers ever see it; the
        // reported line comes from the first real statement, which stays in the
        // parsed tree. There is no parameter list to count, because "args" is
        // implicit.
        var body = SyntaxFactory.Block(statements.Select(statement => statement.Statement));

        return new MemberCandidate(statements[0], body, "<main>", 0);
    }

    private static SourceSummary CreateSourceSummary(CompilationUnitSyntax root, string text)
    {
        var lineCount = CountPhysicalLines(text);
        var classCount = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Count();
        var recordCount = root.DescendantNodes().OfType<RecordDeclarationSyntax>().Count();
        var enumCount = root.DescendantNodes().OfType<EnumDeclarationSyntax>().Count();

        return new SourceSummary(1, lineCount, classCount, recordCount, enumCount);
    }

    private static int CountPhysicalLines(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        return SourceText.From(text).Lines.Count;
    }

    private static string DescribeMethod(BaseMethodDeclarationSyntax method) => method switch
    {
        MethodDeclarationSyntax m => m.Identifier.Text,
        ConstructorDeclarationSyntax c => $"{c.Identifier.Text}.ctor",
        DestructorDeclarationSyntax d => $"~{d.Identifier.Text}",
        OperatorDeclarationSyntax o => $"operator {o.OperatorToken.Text}",
        ConversionOperatorDeclarationSyntax c => $"operator {c.Type}",
        _ => "(member)"
    };

    private static string DescribeAccessor(AccessorDeclarationSyntax accessor)
    {
        var ownerName = accessor.FirstAncestorOrSelf<BasePropertyDeclarationSyntax>() switch
        {
            PropertyDeclarationSyntax property => property.Identifier.Text,
            EventDeclarationSyntax @event => @event.Identifier.Text,
            IndexerDeclarationSyntax => "this[]",
            _ => "(member)"
        };

        return $"{ownerName}.{accessor.Keyword.Text}";
    }

    private static string DescribeContainingType(SyntaxNode node)
    {
        var names = node.Ancestors()
            .OfType<BaseTypeDeclarationSyntax>()
            .Select(type => type.Identifier.Text)
            .Reverse()
            .ToArray();

        return names.Length == 0 ? "<top-level>" : string.Join('.', names);
    }

    /// <summary>
    /// Counts lines that carry code. Blank lines, comment-only lines and lines
    /// holding nothing but a brace do not count.
    /// </summary>
    private static int CountLinesOfCode(SyntaxNode body)
    {
        var count = body.ToString()
            .Split('\n')
            .Select(line => line.Trim())
            .Count(line =>
                line.Length > 0 &&
                line is not "{" and not "}" &&
                !line.StartsWith("//", StringComparison.Ordinal) &&
                !line.StartsWith("/*", StringComparison.Ordinal) &&
                !line.StartsWith('*'));

        return Math.Max(count, 1);
    }

    private readonly record struct MemberCandidate(
        SyntaxNode Declaration,
        SyntaxNode Body,
        string Name,
        int ParameterCount);
}
