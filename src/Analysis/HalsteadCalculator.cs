using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeMetrics.Analysis;

/// <summary>
/// Halstead volume and the maintainability index derived from it.
/// </summary>
/// <remarks>
/// Halstead splits a program into operators and operands. Volume is
/// <c>(total tokens) * log2(distinct tokens)</c>, a size measure that ignores
/// formatting. Classifying C# tokens into the two buckets involves judgement
/// calls, so these numbers land close to Visual Studio's but will not match
/// them exactly. Rank members against each other rather than trusting the
/// absolute value.
/// </remarks>
internal static class HalsteadCalculator
{
    public static double CalculateVolume(SyntaxNode body)
    {
        var distinctOperators = new HashSet<string>(StringComparer.Ordinal);
        var distinctOperands = new HashSet<string>(StringComparer.Ordinal);
        var totalOperators = 0;
        var totalOperands = 0;

        foreach (var token in body.DescendantTokens())
        {
            if (IsOperand(token.Kind()))
            {
                distinctOperands.Add(token.ValueText);
                totalOperands++;
            }
            else if (!IsIgnored(token.Kind()))
            {
                distinctOperators.Add(token.Text);
                totalOperators++;
            }
        }

        var vocabulary = distinctOperators.Count + distinctOperands.Count;
        var length = totalOperators + totalOperands;

        return vocabulary == 0 ? 0d : length * Math.Log2(vocabulary);
    }

    /// <summary>
    /// The rebased Visual Studio formula, clamped to 0 to 100. Higher is better.
    /// Microsoft bands it as 0-9 red, 10-19 yellow, 20+ green.
    /// </summary>
    public static double CalculateMaintainabilityIndex(double halsteadVolume, int cyclomaticComplexity, int linesOfCode)
    {
        var volume = Math.Max(halsteadVolume, 1d);
        var lines = Math.Max(linesOfCode, 1);

        var raw = 171d
                  - (5.2 * Math.Log(volume))
                  - (0.23 * cyclomaticComplexity)
                  - (16.2 * Math.Log(lines));

        return Math.Clamp(raw * 100d / 171d, 0d, 100d);
    }

    private static bool IsOperand(SyntaxKind kind) => kind switch
    {
        SyntaxKind.IdentifierToken => true,
        SyntaxKind.NumericLiteralToken => true,
        SyntaxKind.StringLiteralToken => true,
        SyntaxKind.CharacterLiteralToken => true,
        SyntaxKind.SingleLineRawStringLiteralToken => true,
        SyntaxKind.MultiLineRawStringLiteralToken => true,
        SyntaxKind.InterpolatedStringTextToken => true,
        SyntaxKind.TrueKeyword => true,
        SyntaxKind.FalseKeyword => true,
        SyntaxKind.NullKeyword => true,
        _ => false
    };

    /// <summary>Structural punctuation carries no meaning of its own.</summary>
    private static bool IsIgnored(SyntaxKind kind) => kind switch
    {
        SyntaxKind.SemicolonToken => true,
        SyntaxKind.CommaToken => true,
        SyntaxKind.OpenBraceToken => true,
        SyntaxKind.CloseBraceToken => true,
        SyntaxKind.OpenParenToken => true,
        SyntaxKind.CloseParenToken => true,
        SyntaxKind.EndOfFileToken => true,
        SyntaxKind.None => true,
        _ => false
    };
}
