using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeMetrics.Analysis;

/// <summary>
/// McCabe cyclomatic complexity: the number of linearly independent paths
/// through a member. Starts at 1 and adds 1 per decision point.
/// </summary>
/// <remarks>
/// <para>
/// Each of these adds 1: <c>if</c>, <c>while</c>, <c>do</c>, <c>for</c>,
/// <c>foreach</c>, a <c>case</c> label, a switch expression arm, <c>catch</c>,
/// a catch filter or case guard (<c>when</c>), a conditional expression
/// (<c>?:</c>), a conditional access (<c>?.</c> or <c>?[]</c>), and every
/// <c>&amp;&amp;</c>, <c>||</c>, <c>??</c>, <c>??=</c>, <c>and</c> and
/// <c>or</c>.
/// </para>
/// <para>
/// Compound predicates count once per operator, as McCabe specifies, so
/// <c>a &amp;&amp; b</c> is two decisions. Pattern combinators follow the same
/// rule, which keeps <c>x is 1 or 2</c> level with <c>x == 1 || x == 2</c>.
/// </para>
/// <para>
/// These add nothing, because none of them chooses between paths:
/// <c>else</c>, <c>default:</c>, a bare discard (<c>case _:</c> or
/// <c>_ =&gt;</c>), <c>not</c>, <c>try</c>, <c>finally</c> and <c>goto</c>.
/// An <c>else</c> and a discard are the fall-through, and a <c>goto</c> is an
/// unconditional jump.
/// </para>
/// <para>
/// Known approximations. Lambdas and local functions fold into the containing
/// member, which is what Visual Studio does; SonarQube scores them separately.
/// Property and recursive patterns are not decomposed, so
/// <c>x is { A: 1, B: 2 }</c> scores 1 where the equivalent
/// <c>x.A == 1 &amp;&amp; x.B == 2</c> scores 2. LINQ query clauses are not
/// counted. In a case label a bare <c>_</c> is read as the discard rather than
/// as a constant of that name, which syntax alone cannot distinguish.
/// </para>
/// </remarks>
public sealed class CyclomaticComplexityWalker : CSharpSyntaxWalker
{
    public int Complexity { get; private set; } = 1;

    public override void VisitIfStatement(IfStatementSyntax node)
    {
        // "else" adds no path of its own, only "if" does.
        Complexity++;
        base.VisitIfStatement(node);
    }

    public override void VisitWhileStatement(WhileStatementSyntax node)
    {
        Complexity++;
        base.VisitWhileStatement(node);
    }

    public override void VisitDoStatement(DoStatementSyntax node)
    {
        Complexity++;
        base.VisitDoStatement(node);
    }

    public override void VisitForStatement(ForStatementSyntax node)
    {
        Complexity++;
        base.VisitForStatement(node);
    }

    public override void VisitForEachStatement(ForEachStatementSyntax node)
    {
        Complexity++;
        base.VisitForEachStatement(node);
    }

    public override void VisitForEachVariableStatement(ForEachVariableStatementSyntax node)
    {
        Complexity++;
        base.VisitForEachVariableStatement(node);
    }

    public override void VisitCaseSwitchLabel(CaseSwitchLabelSyntax node)
    {
        // "case _:" arrives here, not as a pattern label. See IsCatchAll.
        if (!IsDiscard(node.Value))
        {
            Complexity++;
        }

        base.VisitCaseSwitchLabel(node);
    }

    public override void VisitCasePatternSwitchLabel(CasePatternSwitchLabelSyntax node)
    {
        // A guarded discard, "case _ when g:", reaches here. The discard adds
        // nothing; the guard still scores, through VisitWhenClause.
        if (!IsCatchAll(node.Pattern))
        {
            Complexity++;
        }

        base.VisitCasePatternSwitchLabel(node);
    }

    public override void VisitSwitchExpressionArm(SwitchExpressionArmSyntax node)
    {
        if (!IsCatchAll(node.Pattern))
        {
            Complexity++;
        }

        base.VisitSwitchExpressionArm(node);
    }

    /// <summary>
    /// <c>and</c> and <c>or</c> are compound predicates, exactly like
    /// <c>&amp;&amp;</c> and <c>||</c>. <c>not</c> is a
    /// <see cref="UnaryPatternSyntax"/> and stays free, because negating one
    /// test opens no new path.
    /// </summary>
    public override void VisitBinaryPattern(BinaryPatternSyntax node)
    {
        // The only two kinds are AndPattern and OrPattern, and both count.
        Complexity++;
        base.VisitBinaryPattern(node);
    }

    public override void VisitCatchClause(CatchClauseSyntax node)
    {
        Complexity++;
        base.VisitCatchClause(node);
    }

    /// <summary>
    /// The <c>when</c> on a catch is its own node kind, separate from the
    /// <c>when</c> on a case. Filtering decides whether the handler runs, so it
    /// scores like a case guard.
    /// </summary>
    public override void VisitCatchFilterClause(CatchFilterClauseSyntax node)
    {
        Complexity++;
        base.VisitCatchFilterClause(node);
    }

    /// <summary>Case guards, on both switch statements and switch expressions.</summary>
    public override void VisitWhenClause(WhenClauseSyntax node)
    {
        Complexity++;
        base.VisitWhenClause(node);
    }

    public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
    {
        Complexity++;
        base.VisitConditionalExpression(node);
    }

    public override void VisitConditionalAccessExpression(ConditionalAccessExpressionSyntax node)
    {
        Complexity++;
        base.VisitConditionalAccessExpression(node);
    }

    public override void VisitBinaryExpression(BinaryExpressionSyntax node)
    {
        if (node.IsKind(SyntaxKind.LogicalAndExpression) ||
            node.IsKind(SyntaxKind.LogicalOrExpression) ||
            node.IsKind(SyntaxKind.CoalesceExpression))
        {
            Complexity++;
        }

        base.VisitBinaryExpression(node);
    }

    public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
    {
        if (node.IsKind(SyntaxKind.CoalesceAssignmentExpression))
        {
            Complexity++;
        }

        base.VisitAssignmentExpression(node);
    }

    /// <summary>
    /// A discard matches everything, so it is the fall-through rather than a
    /// branch. Keeps <c>case _:</c> level with <c>default:</c> and <c>_ =&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Roslyn only produces a <see cref="DiscardPatternSyntax"/> where a discard
    /// is unambiguous. In a case label <c>_</c> stays an identifier, because
    /// before C# 9 it could name a constant. Syntax alone cannot separate the
    /// two, so a bare <c>_</c> is read as the discard, which is overwhelmingly
    /// what it means. An escaped <c>@_</c> is left as a real constant.
    /// </remarks>
    private static bool IsCatchAll(PatternSyntax pattern) => pattern switch
    {
        DiscardPatternSyntax => true,
        ConstantPatternSyntax constant => IsDiscard(constant.Expression),
        _ => false
    };

    private static bool IsDiscard(ExpressionSyntax? expression) =>
        expression is IdentifierNameSyntax { Identifier.Text: "_" };
}
