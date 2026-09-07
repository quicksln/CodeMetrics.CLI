// Not compiled into the tool. Run "codemetrics ./samples" and compare the
// output against the expected values in the comments.
//
// Expected:
//   SumOfPrimes   cognitive 7   cyclomatic 4
//   Describe      cognitive 1   cyclomatic 4
//   IsEligible    cognitive 3   cyclomatic 5
//   Grade         cognitive 4   cyclomatic 4
//   Classify      cognitive 1   cyclomatic 6
//   Label         cognitive 0   cyclomatic 4
//   Process       cognitive 9   cyclomatic 5

using System;
using System.Collections.Generic;

namespace Samples;

public static class ComplexitySamples
{
    // for +1, nested for +2, nested if +3, goto +1 = 7
    public static int SumOfPrimes(int max)
    {
        var total = 0;

        for (var i = 2; i <= max; i++)
        {
            for (var j = 2; j < i; j++)
            {
                if (i % j == 0)
                {
                    goto nextCandidate;
                }
            }

            total += i;

            nextCandidate: ;
        }

        return total;
    }

    // Wide but flat. Four paths to test, one thing to read.
    // The whole switch is a single cognitive increment.
    public static string Describe(DayOfWeek day) => day switch
    {
        DayOfWeek.Saturday => "weekend",
        DayOfWeek.Sunday => "weekend",
        DayOfWeek.Monday => "start of the week",
        _ => "weekday"
    };

    // if +1, then one increment per run of like operators: (&& &&) then (||) = 2.
    public static bool IsEligible(bool a, bool b, bool c, bool d)
    {
        if (a && b && c || d)
        {
            return true;
        }

        return false;
    }

    // else-if costs 1 each with no nesting penalty, which is why chains beat nests.
    public static string Grade(int score)
    {
        if (score >= 90)
        {
            return "A";
        }
        else if (score >= 80)
        {
            return "B";
        }
        else if (score >= 70)
        {
            return "C";
        }
        else
        {
            return "F";
        }
    }

    // Pattern combinators are decision points, so this scores the same as the
    // comparison chain it replaces. Cognitively a whole switch is still 1.
    public static string Classify(int value) => value switch
    {
        1 or 2 or 3 => "low",
        > 10 and < 20 => "mid",
        _ => "other"
    };

    // Null shorthand is where the two metrics disagree hardest: every ?. and ??
    // is a path to test, but none of them costs anything to read.
    public static string Label(string? text, string? fallback)
    {
        return text?.Trim() ?? fallback ?? "unknown";
    }

    // foreach +1, if +2, inner if +3, catch +3 = 9.
    // try does not raise the nesting level, catch does.
    public static void Process(IEnumerable<string> items)
    {
        foreach (var item in items)
        {
            if (item.Length > 0)
            {
                try
                {
                    if (item.StartsWith('#'))
                    {
                        Console.WriteLine(item);
                    }
                }
                catch (FormatException)
                {
                    Console.Error.WriteLine("bad item");
                }
            }
        }
    }
}
