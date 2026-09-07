using CodeMetrics.Cli;
using Xunit;

namespace CodeMetrics.Net.Tests;

/// <summary>
/// Option names, aliases, defaults, validation messages and the single-file
/// sidecar naming are all published contracts, so each one gets a case. The
/// error strings are asserted verbatim because users read them.
/// </summary>
public class OptionsParseTests
{
    [Fact]
    public void PositionalPath_UsesDocumentedDefaults()
    {
        var options = Parse("./src");

        Assert.Equal("./src", options.Path);
        Assert.Null(options.SingleFile);
        Assert.Equal(OutputFormat.Table, options.Format);
        Assert.Equal(20, options.Top);
        Assert.Equal(0, options.MaxCyclomatic);
        Assert.Equal(0, options.MaxCognitive);
        Assert.Null(options.OutputFile);
        Assert.False(options.HasGate);
    }

    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    public void HelpFlag_ReturnsNoOptionsAndNoError(string flag)
    {
        // No options plus no error is what makes the tool print usage to stdout
        // and exit 0; an error would send it to stderr and exit 2.
        var options = Options.Parse([flag], out var error);

        Assert.Null(options);
        Assert.Null(error);
    }

    [Fact]
    public void NoArguments_IsTreatedAsAHelpRequest()
    {
        var options = Options.Parse([], out var error);

        Assert.Null(options);
        Assert.Null(error);
    }

    [Fact]
    public void Help_IsOnlyRecognisedAsTheFirstArgument()
    {
        // "--help" later in the line falls through to the unknown-option branch.
        Options.Parse(["./src", "--help"], out var error);

        Assert.Equal("Unknown option '--help'.", error);
    }

    [Theory]
    [InlineData("table", OutputFormat.Table)]
    [InlineData("csv", OutputFormat.Csv)]
    [InlineData("json", OutputFormat.Json)]
    [InlineData("html", OutputFormat.Html)]
    // Format names are case-insensitive.
    [InlineData("JSON", OutputFormat.Json)]
    [InlineData("Html", OutputFormat.Html)]
    public void Format_ParsesEveryDocumentedName(string value, OutputFormat expected)
    {
        Assert.Equal(expected, Parse("--format", value, "./src").Format);
        Assert.Equal(expected, Parse("-f", value, "./src").Format);
    }

    [Theory]
    [InlineData("--top", "40")]
    [InlineData("-t", "40")]
    public void Top_ParsesBothAliases(string flag, string value)
    {
        Assert.Equal(40, Parse(flag, value, "./src").Top);
    }

    [Fact]
    public void Gates_ParseIndependentlyAndDriveHasGate()
    {
        var cyclomatic = Parse("--max-cyclomatic", "10", "./src");
        Assert.Equal(10, cyclomatic.MaxCyclomatic);
        Assert.Equal(0, cyclomatic.MaxCognitive);
        Assert.True(cyclomatic.HasGate);

        var cognitive = Parse("--max-cognitive", "15", "./src");
        Assert.Equal(15, cognitive.MaxCognitive);
        Assert.Equal(0, cognitive.MaxCyclomatic);
        Assert.True(cognitive.HasGate);

        var both = Parse("--max-cyclomatic", "7", "--max-cognitive", "15", "./src");
        Assert.Equal(7, both.MaxCyclomatic);
        Assert.Equal(15, both.MaxCognitive);
        Assert.True(both.HasGate);
    }

    [Theory]
    // Zero is the documented way to disable a gate, so it must not enable one.
    [InlineData("0", false)]
    [InlineData("-1", false)]
    [InlineData("1", true)]
    public void HasGate_TracksOnlyPositiveLimits(string limit, bool expected)
    {
        Assert.Equal(expected, Parse("--max-cognitive", limit, "./src").HasGate);
    }

    [Theory]
    [InlineData("--output")]
    [InlineData("-o")]
    public void Output_ParsesBothAliases(string flag)
    {
        Assert.Equal("metrics.json", Parse(flag, "metrics.json", "./src").OutputFile);
    }

    [Theory]
    // A value-taking option at the end of the line has nothing to consume.
    [InlineData(new[] { "--format" }, "--format needs a value.")]
    [InlineData(new[] { "-f" }, "--format needs a value.")]
    [InlineData(new[] { "--top" }, "--top needs a whole number.")]
    [InlineData(new[] { "--max-cyclomatic" }, "--max-cyclomatic needs a whole number.")]
    [InlineData(new[] { "--max-cognitive" }, "--max-cognitive needs a whole number.")]
    [InlineData(new[] { "--single" }, "--single needs a file path.")]
    [InlineData(new[] { "--output" }, "--output needs a file path.")]
    // Values that are the wrong shape.
    [InlineData(new[] { "-f", "xml", "./src" }, "Unknown format 'xml'. Use table, csv, json or html.")]
    [InlineData(new[] { "--top", "abc", "./src" }, "--top needs a whole number.")]
    [InlineData(new[] { "--max-cognitive", "ten", "./src" }, "--max-cognitive needs a whole number.")]
    // Shape of the command line itself.
    [InlineData(new[] { "-x", "./src" }, "Unknown option '-x'.")]
    [InlineData(new[] { "./src", "./other" }, "Only one path can be analysed per run.")]
    [InlineData(new[] { "--top", "5" }, "A path is required.")]
    [InlineData(new[] { "-s", "a.cs", "-s", "b.cs" }, "Only one file can be passed with -s.")]
    [InlineData(
        new[] { "-s", "a.cs", "./src" },
        "-s and a positional path cannot be combined. Pass either a path or -s <file>.")]
    public void InvalidArguments_ReportTheDocumentedMessage(string[] args, string expected)
    {
        var options = Options.Parse(args, out var error);

        Assert.Null(options);
        Assert.Equal(expected, error);
    }

    [Fact]
    public void SingleFile_DefaultsToJsonAndBecomesThePath()
    {
        // Single-file mode targets agents consuming the report, so json wins
        // unless the caller asks for something else.
        var options = Parse("-s", "src/Checkout.cs");

        Assert.Equal("src/Checkout.cs", options.SingleFile);
        Assert.Equal("src/Checkout.cs", options.Path);
        Assert.Equal(OutputFormat.Json, options.Format);
    }

    [Fact]
    public void SingleFile_KeepsAnExplicitFormat()
    {
        Assert.Equal(OutputFormat.Csv, Parse("-s", "src/Checkout.cs", "-f", "csv").Format);
        Assert.Equal(OutputFormat.Table, Parse("-f", "table", "-s", "src/Checkout.cs").Format);
    }

    [Theory]
    // The sidecar is named <source>.codemetrics.<ext>, next to the source file.
    [InlineData("json", ".codemetrics.json")]
    [InlineData("csv", ".codemetrics.csv")]
    [InlineData("html", ".codemetrics.html")]
    [InlineData("table", ".codemetrics.txt")]
    public void SingleFile_NamesTheSidecarAfterTheSourceAndFormat(string format, string expectedSuffix)
    {
        var source = Path.Combine(Path.GetTempPath(), "Checkout.cs");
        var options = Parse("-s", source, "-f", format);

        var expected = Path.Combine(Path.GetTempPath(), $"Checkout{expectedSuffix}");

        Assert.Equal(expected, options.OutputFile);
    }

    [Fact]
    public void SingleFile_SidecarPathIsAbsoluteEvenForARelativeSource()
    {
        // The point of the default is that the location is predictable from any
        // working directory.
        var options = Parse("-s", "src/Checkout.cs");

        Assert.NotNull(options.OutputFile);
        Assert.True(Path.IsPathFullyQualified(options.OutputFile), options.OutputFile);
        Assert.EndsWith("Checkout.codemetrics.json", options.OutputFile, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplicitOutput_WinsOverTheSidecarDefault()
    {
        // An explicit -o relocates the copy whichever side of -s it appears on.
        Assert.Equal("custom.json", Parse("-s", "src/Checkout.cs", "-o", "custom.json").OutputFile);
        Assert.Equal("custom.json", Parse("-o", "custom.json", "-s", "src/Checkout.cs").OutputFile);
    }

    [Fact]
    public void OptionsMayPrecedeOrFollowThePath()
    {
        var before = Parse("--format", "csv", "--top", "5", "./src");
        var after = Parse("./src", "--format", "csv", "--top", "5");

        Assert.Equal(before, after);
    }

    [Fact]
    public void PrintUsage_DocumentsBothInvocationsAndEveryExitCode()
    {
        var buffer = new StringWriter();
        Options.PrintUsage(buffer);

        var usage = buffer.ToString();

        Assert.Contains("codemetrics <path> [options]", usage, StringComparison.Ordinal);
        Assert.Contains("codemetrics -s <file.cs> [options]", usage, StringComparison.Ordinal);
        Assert.Contains("0  clean", usage, StringComparison.Ordinal);
        Assert.Contains("1  a threshold was exceeded", usage, StringComparison.Ordinal);
        Assert.Contains("2  bad arguments or nothing to analyse", usage, StringComparison.Ordinal);

        // Every option the parser accepts must be discoverable from the help.
        foreach (var flag in new[]
                 {
                     "--format", "--top", "--output", "--max-cyclomatic", "--max-cognitive", "--single", "--help"
                 })
        {
            Assert.Contains(flag, usage, StringComparison.Ordinal);
        }
    }

    private static Options Parse(params string[] args)
    {
        var options = Options.Parse(args, out var error);

        Assert.Null(error);
        Assert.NotNull(options);

        return options;
    }
}
