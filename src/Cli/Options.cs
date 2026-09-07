namespace CodeMetrics.Cli;

public enum OutputFormat
{
    Table,
    Csv,
    Json,
    Html
}

/// <summary>
/// Hand-rolled argument parsing. The surface is small enough that a parser
/// library would cost more in dependency weight than it saves in code.
/// </summary>
public sealed record Options
{
    public required string Path { get; init; }

    /// <summary>Set when the user passes -s. Single-file mode.</summary>
    public string? SingleFile { get; init; }

    public OutputFormat Format { get; init; } = OutputFormat.Table;

    /// <summary>How many of the worst members to print. Only affects the table.</summary>
    public int Top { get; init; } = 20;

    /// <summary>Fail the run above this cyclomatic score. 0 disables the gate.</summary>
    public int MaxCyclomatic { get; init; }

    /// <summary>Fail the run above this cognitive score. 0 disables the gate.</summary>
    public int MaxCognitive { get; init; }

    public string? OutputFile { get; init; }

    public bool HasGate => MaxCyclomatic > 0 || MaxCognitive > 0;

    public static Options? Parse(string[] args, out string? error)
    {
        error = null;

        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            return null;
        }

        string? path = null;
        string? outputFile = null;
        string? singleFile = null;
        var format = OutputFormat.Table;
        var formatSpecified = false;
        var top = 20;
        var maxCyclomatic = 0;
        var maxCognitive = 0;

        for (var i = 0; i < args.Length; i++)
        {
            var argument = args[i];

            switch (argument)
            {
                case "--format" or "-f":
                    if (!TryTakeValue(args, ref i, out var formatValue))
                    {
                        error = "--format needs a value.";
                        return null;
                    }

                    if (!Enum.TryParse(formatValue, ignoreCase: true, out format))
                    {
                        error = $"Unknown format '{formatValue}'. Use table, csv, json or html.";
                        return null;
                    }

                    formatSpecified = true;
                    break;

                case "--top" or "-t":
                    if (!TryTakeInt(args, ref i, out top))
                    {
                        error = "--top needs a whole number.";
                        return null;
                    }

                    break;

                case "--max-cyclomatic":
                    if (!TryTakeInt(args, ref i, out maxCyclomatic))
                    {
                        error = "--max-cyclomatic needs a whole number.";
                        return null;
                    }

                    break;

                case "--max-cognitive":
                    if (!TryTakeInt(args, ref i, out maxCognitive))
                    {
                        error = "--max-cognitive needs a whole number.";
                        return null;
                    }

                    break;

                case "--single" or "-s":
                    if (!TryTakeValue(args, ref i, out var singleValue))
                    {
                        error = "--single needs a file path.";
                        return null;
                    }

                    if (singleFile is not null)
                    {
                        error = "Only one file can be passed with -s.";
                        return null;
                    }

                    singleFile = singleValue;
                    break;

                case "--output" or "-o":
                    if (!TryTakeValue(args, ref i, out outputFile))
                    {
                        error = "--output needs a file path.";
                        return null;
                    }

                    break;

                default:
                    if (argument.StartsWith('-'))
                    {
                        error = $"Unknown option '{argument}'.";
                        return null;
                    }

                    if (path is not null)
                    {
                        error = "Only one path can be analysed per run.";
                        return null;
                    }

                    path = argument;
                    break;
            }
        }

        if (singleFile is not null)
        {
            if (path is not null)
            {
                error = "-s and a positional path cannot be combined. Pass either a path or -s <file>.";
                return null;
            }

            // Single-file mode is aimed at agents consuming the report, so json
            // is the default; -f can still opt in to the human-friendly table.
            // The report goes to stdout and a copy is saved next to the source
            // file, so the location is predictable from any working directory.
            path = singleFile;

            if (!formatSpecified)
            {
                format = OutputFormat.Json;
            }

            outputFile ??= DefaultReportPath(singleFile, format);
        }

        if (path is null)
        {
            error = "A path is required.";
            return null;
        }

        return new Options
        {
            Path = path,
            SingleFile = singleFile,
            Format = format,
            Top = top,
            MaxCyclomatic = maxCyclomatic,
            MaxCognitive = maxCognitive,
            OutputFile = outputFile
        };
    }

    public static void PrintUsage(TextWriter writer)
    {
        writer.WriteLine("""
            codemetrics - complexity metrics for C# code

            Usage:
              codemetrics <path> [options]
              codemetrics -s <file.cs> [options]

            <path> may be a .sln, .slnx, .csproj, a directory or a single .cs file.
            -s <file.cs> analyses exactly one C# file, prints the report to
            stdout and writes a copy next to the file. Default format: json.

            Options:
              -f, --format <table|csv|json|html>  Output format. Default: table.
              -t, --top <n>                      Rows in the table. Default: 20.
              -o, --output <file>                Write to a file instead of stdout.
                                                 With -s, relocates the file copy.
                  --max-cyclomatic <n>           Exit 1 if any member exceeds this.
                  --max-cognitive <n>            Exit 1 if any member exceeds this.
              -s, --single <file.cs>             Single-file mode: report to stdout
                                                 plus a copy next to the file.
              -h, --help                         Show this help.

            Exit codes:
              0  clean
              1  a threshold was exceeded
              2  bad arguments or nothing to analyse

            Examples:
              codemetrics ./src
              codemetrics MyApp.sln --top 40
              codemetrics MyApp.sln --format json --output metrics.json
              codemetrics ./src --format html --output metrics-report.html
              codemetrics ./src --max-cognitive 15 --max-cyclomatic 10
              codemetrics -s src/Checkout.cs
              codemetrics -s src/Checkout.cs -f csv
            """);
    }

    private static bool TryTakeValue(string[] args, ref int index, out string value)
    {
        if (index + 1 >= args.Length)
        {
            value = string.Empty;
            return false;
        }

        index++;
        value = args[index];
        return true;
    }

    private static bool TryTakeInt(string[] args, ref int index, out int value)
    {
        value = 0;
        return TryTakeValue(args, ref index, out var raw) && int.TryParse(raw, out value);
    }

    /// <summary>
    /// Default location of the report copy in single-file mode: next to the
    /// source, named &lt;source&gt;.codemetrics.&lt;ext&gt;. Predictable no matter which
    /// directory the tool is run from.
    /// </summary>
    private static string DefaultReportPath(string sourceFile, OutputFormat format)
    {
        var extension = format switch
        {
            OutputFormat.Csv => ".csv",
            OutputFormat.Html => ".html",
            OutputFormat.Table => ".txt",
            _ => ".json"
        };

        // Fully qualify System.IO.Path — the record's own Path property
        // shadows it inside this class.
        var fullSourcePath = System.IO.Path.GetFullPath(sourceFile);
        var directory = System.IO.Path.GetDirectoryName(fullSourcePath)!;
        var fileName = $"{System.IO.Path.GetFileNameWithoutExtension(fullSourcePath)}.codemetrics{extension}";

        return System.IO.Path.Combine(directory, fileName);
    }
}
