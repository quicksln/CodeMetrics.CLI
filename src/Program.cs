using System.Diagnostics;
using CodeMetrics.Analysis;
using CodeMetrics.Cli;
using CodeMetrics.Reporting;

var options = Options.Parse(args, out var parseError);

if (options is null)
{
    if (parseError is not null)
    {
        Console.Error.WriteLine(parseError);
        Console.Error.WriteLine();
    }

    Options.PrintUsage(parseError is null ? Console.Out : Console.Error);
    return parseError is null ? 0 : 2;
}

var stopwatch = Stopwatch.StartNew();

IReadOnlyList<string> files;

if (options.SingleFile is not null)
{
    if (!File.Exists(options.SingleFile))
    {
        Console.Error.WriteLine($"File not found: '{options.SingleFile}'.");
        return 2;
    }

    if (!Path.GetExtension(options.SingleFile).Equals(".cs", StringComparison.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine($"-s needs a single .cs file; got '{options.SingleFile}'.");
        return 2;
    }

    // An explicit -s file: no globbing, no generated-file filtering — the
    // user named exactly what they want analysed.
    files = [Path.GetFullPath(options.SingleFile)];
}
else
{
    files = SourceFileLocator.Locate(options.Path);

    if (files.Count == 0)
    {
        Console.Error.WriteLine($"No C# files found at '{options.Path}'.");
        return 2;
    }
}

// Parsing is CPU bound and every file is independent, so fan out across cores.
var fileResults = files
    .Select((file, index) => (Index: index, File: file))
    .AsParallel()
    .Select(item => (item.Index, Result: MetricsAnalyzer.AnalyzeFile(item.File)))
    .ToList()
    .OrderBy(item => item.Index)
    .Select(item => item.Result)
    .ToList();

var members = fileResults
    .Where(result => result.SourceSummary is not null)
    .SelectMany(result => result.Members)
    .ToList();

var sourceSummary = fileResults
    .Where(result => result.SourceSummary is not null)
    .Select(result => result.SourceSummary!)
    .Aggregate(SourceSummary.Empty, (current, next) => current.Combine(next));

var report = new AnalysisReport(members, sourceSummary);

stopwatch.Stop();

if (options.SingleFile is not null)
{
    // Single-file mode for agent skills: the identical report goes to stdout
    // (so callers can parse it directly) and to the file next to the source.
    // Build it once, write it twice. Status goes to stderr so stdout stays
    // machine-readable.
    var buffer = new StringWriter();
    Reporters.Write(report, options, buffer);
    var reportText = buffer.ToString();

    await Console.Out.WriteAsync(reportText);

    await using (var fileOutput = new StreamWriter(options.OutputFile!))
    {
        await fileOutput.WriteAsync(reportText);
    }

    Console.Error.WriteLine($"Wrote {members.Count} member(s) to {options.OutputFile} in {stopwatch.ElapsedMilliseconds} ms.");
}
else
{
    await using var output = options.OutputFile is null
        ? TextWriter.Null
        : new StreamWriter(options.OutputFile);

    var writer = options.OutputFile is null ? Console.Out : output;
    Reporters.Write(report, options, writer);

    if (options.OutputFile is not null)
    {
        Console.Out.WriteLine($"Wrote {members.Count} member(s) to {options.OutputFile} in {stopwatch.ElapsedMilliseconds} ms.");
    }
    else if (options.Format == OutputFormat.Table)
    {
        Console.Out.WriteLine();
        Console.Out.WriteLine($"Done in {stopwatch.ElapsedMilliseconds} ms.");
    }
}

var overCyclomatic = options.MaxCyclomatic > 0 &&
                     members.Any(m => m.CyclomaticComplexity > options.MaxCyclomatic);

var overCognitive = options.MaxCognitive > 0 &&
                    members.Any(m => m.CognitiveComplexity > options.MaxCognitive);

return overCyclomatic || overCognitive ? 1 : 0;
