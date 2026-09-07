using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeMetrics.Analysis;

/// <summary>
/// Cognitive complexity as defined by SonarSource: how hard a member is to
/// read, rather than how many paths it has.
/// </summary>
/// <remarks>
/// Three rules drive the score.
/// 1. Structures that break linear reading flow score +1 (if, loops, catch,
///    switch, ternary).
/// 2. Nesting those structures inside each other adds +1 per level.
/// 3. Shorthand that condenses lines is free, so <c>??</c>, <c>?.</c> and
///    <c>?[]</c> cost nothing here even though cyclomatic complexity counts them.
/// A whole switch scores 1 no matter how many cases it has, because comparing
/// one value against a list of literals is easy to read.
/// </remarks>
public sealed class CognitiveComplexityWalker : CSharpSyntaxWalker
{
    private readonly string _memberName;
    private int _nesting;

    public CognitiveComplexityWalker(string memberName) => _memberName = memberName;

    public int Complexity { get; private set; }

    public int MaxNestingDepth { get; private set; }

    private void Increment(bool withNesting) => Complexity += withNesting ? 1 + _nesting : 1;

    private void EnterNesting()
    {
        _nesting++;

        if (_nesting > MaxNestingDepth)
        {
            MaxNestingDepth = _nesting;
        }
    }

    private void ExitNesting() => _nesting--;

    private void VisitNested(SyntaxNode? node)
    {
        if (node is null)
        {
            return;
        }

        EnterNesting();
        Visit(node);
        ExitNesting();
    }

    public override void VisitIfStatement(IfStatementSyntax node)
    {
        // "else if" is a hybrid: flat +1, but its body still sits one level deeper.
        var isElseIf = node.Parent is ElseClauseSyntax;

        Increment(withNesting: !isElseIf);
        Visit(node.Condition);
        VisitNested(node.Statement);

        if (node.Else is null)
        {
            return;
        }

        if (node.Else.Statement is IfStatementSyntax chainedIf)
        {
            Visit(chainedIf);
        }
        else
        {
            Increment(withNesting: false);
            VisitNested(node.Else.Statement);
        }
    }

    public override void VisitWhileStatement(WhileStatementSyntax node)
    {
        Increment(withNesting: true);
        Visit(node.Condition);
        VisitNested(node.Statement);
    }

    public override void VisitDoStatement(DoStatementSyntax node)
    {
        Increment(withNesting: true);
        VisitNested(node.Statement);
        Visit(node.Condition);
    }

    public override void VisitForStatement(ForStatementSyntax node)
    {
        Increment(withNesting: true);
        Visit(node.Declaration);

        foreach (var initializer in node.Initializers)
        {
            Visit(initializer);
        }

        Visit(node.Condition);

        foreach (var incrementor in node.Incrementors)
        {
            Visit(incrementor);
        }

        VisitNested(node.Statement);
    }

    public override void VisitForEachStatement(ForEachStatementSyntax node)
    {
        Increment(withNesting: true);
        Visit(node.Expression);
        VisitNested(node.Statement);
    }

    public override void VisitForEachVariableStatement(ForEachVariableStatementSyntax node)
    {
        Increment(withNesting: true);
        Visit(node.Expression);
        VisitNested(node.Statement);
    }

    public override void VisitSwitchStatement(SwitchStatementSyntax node)
    {
        Increment(withNesting: true);
        Visit(node.Expression);

        EnterNesting();

        foreach (var section in node.Sections)
        {
            Visit(section);
        }

        ExitNesting();
    }

    public override void VisitSwitchExpression(SwitchExpressionSyntax node)
    {
        Increment(withNesting: true);
        Visit(node.GoverningExpression);

        EnterNesting();

        foreach (var arm in node.Arms)
        {
            Visit(arm);
        }

        ExitNesting();
    }

    public override void VisitCatchClause(CatchClauseSyntax node)
    {
        Increment(withNesting: true);
        Visit(node.Declaration);
        Visit(node.Filter);
        VisitNested(node.Block);
    }

    public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
    {
        Increment(withNesting: true);
        Visit(node.Condition);

        EnterNesting();
        Visit(node.WhenTrue);
        Visit(node.WhenFalse);
        ExitNesting();
    }

    /// <summary>
    /// A run of the same boolean operator costs 1, no matter how long it is.
    /// Mixing operators costs 1 per run, so <c>a &amp;&amp; b || c</c> scores 2.
    /// Parentheses start a fresh run because the recursion stops at them and the
    /// inner expression is visited on its own.
    /// </summary>
    public override void VisitBinaryExpression(BinaryExpressionSyntax node)
    {
        if (IsLogical(node) && !IsLogical(node.Parent))
        {
            Complexity += CountOperatorRuns(node);
        }

        base.VisitBinaryExpression(node);
    }

    public override void VisitGotoStatement(GotoStatementSyntax node)
    {
        // A jump to a label is a fundamental increment: flat +1, no nesting penalty.
        Increment(withNesting: false);
        base.VisitGotoStatement(node);
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (IsRecursiveCall(node))
        {
            Increment(withNesting: false);
        }

        base.VisitInvocationExpression(node);
    }

    public override void VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
    {
        // Declaring a lambda is free, but its body reads as one level deeper.
        Visit(node.Parameter);
        VisitNested(node.Body);
    }

    public override void VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
    {
        Visit(node.ParameterList);
        VisitNested(node.Body);
    }

    public override void VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
    {
        Visit(node.ParameterList);
        VisitNested(node.Body);
    }

    public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
    {
        Visit(node.ParameterList);
        VisitNested((SyntaxNode?)node.Body ?? node.ExpressionBody);
    }

    private bool IsRecursiveCall(InvocationExpressionSyntax node)
    {
        var invoked = node.Expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            GenericNameSyntax generic => generic.Identifier.Text,
            MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } access => access.Name.Identifier.Text,
            _ => null
        };

        return invoked is not null && string.Equals(invoked, _memberName, StringComparison.Ordinal);
    }

    private static bool IsLogical(SyntaxNode? node) =>
        node is BinaryExpressionSyntax binary &&
        (binary.IsKind(SyntaxKind.LogicalAndExpression) || binary.IsKind(SyntaxKind.LogicalOrExpression));

    private static int CountOperatorRuns(BinaryExpressionSyntax root)
    {
        var operators = new List<SyntaxKind>();
        Flatten(root, operators);

        var runs = 0;
        var previous = SyntaxKind.None;

        foreach (var kind in operators)
        {
            if (kind != previous)
            {
                runs++;
            }

            previous = kind;
        }

        return runs;
    }

    private static void Flatten(ExpressionSyntax expression, List<SyntaxKind> operators)
    {
        if (expression is not BinaryExpressionSyntax binary || !IsLogical(binary))
        {
            return;
        }

        Flatten(binary.Left, operators);
        operators.Add(binary.Kind());
        Flatten(binary.Right, operators);
    }
}
