using System.Globalization;
using System.Text;
using System.Text.Json;
using CodeMetrics.Analysis;
using CodeMetrics.Cli;

namespace CodeMetrics.Reporting;

public static class Reporters
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void Write(
        AnalysisReport report,
        Options options,
        TextWriter writer)
    {
        switch (options.Format)
        {
            case OutputFormat.Json:
                writer.WriteLine(JsonSerializer.Serialize(report.Members, JsonOptions));
                break;

            case OutputFormat.Csv:
                WriteCsv(report.Members, writer);
                break;

            case OutputFormat.Html:
                WriteHtml(report, options.Path, writer);
                break;

            default:
                WriteTable(report.Members, options, report.SourceSummary.FileCount, writer);
                break;
        }
    }

    public static void Write(
        AnalysisReport report,
        Options options,
        int fileCount,
        TextWriter writer)
    {
        Write(report, options, writer);
    }

    public static void Write(
        IReadOnlyList<MemberMetrics> members,
        Options options,
        int fileCount,
        TextWriter writer)
    {
        switch (options.Format)
        {
            case OutputFormat.Json:
                writer.WriteLine(JsonSerializer.Serialize(members, JsonOptions));
                break;

            case OutputFormat.Csv:
                WriteCsv(members, writer);
                break;

            case OutputFormat.Html:
                WriteHtml(new AnalysisReport(members, new SourceSummary(fileCount, 0, 0, 0, 0)), options.Path, writer);
                break;

            default:
                WriteTable(members, options, fileCount, writer);
                break;
        }
    }

    private static void WriteTable(
        IReadOnlyList<MemberMetrics> members,
        Options options,
        int fileCount,
        TextWriter writer)
    {
        var ranked = members
            .OrderByDescending(m => m.CognitiveComplexity)
            .ThenByDescending(m => m.CyclomaticComplexity)
            .Take(options.Top)
            .ToList();

        writer.WriteLine();
        writer.WriteLine($"Analysed {fileCount} file(s), {members.Count} member(s).");
        writer.WriteLine();

        if (ranked.Count == 0)
        {
            writer.WriteLine("No members with a body were found.");
            return;
        }

        var rows = ranked.Select(m => new[]
        {
            m.CognitiveComplexity.ToString(CultureInfo.InvariantCulture),
            m.CyclomaticComplexity.ToString(CultureInfo.InvariantCulture),
            m.LinesOfCode.ToString(CultureInfo.InvariantCulture),
            m.MaxNestingDepth.ToString(CultureInfo.InvariantCulture),
            m.MaintainabilityIndex.ToString("F0", CultureInfo.InvariantCulture),
            Truncate(m.FullName, 48),
            $"{Path.GetFileName(m.FilePath)}:{m.LineNumber}"
        }).ToList();

        string[] headers = ["Cog", "Cyc", "LOC", "Nest", "MI", "Member", "Location"];
        var widths = headers
            .Select((header, column) => Math.Max(header.Length, rows.Max(row => row[column].Length)))
            .ToArray();

        writer.WriteLine(FormatRow(headers, widths));
        writer.WriteLine(string.Join("  ", widths.Select(width => new string('-', width))));

        foreach (var row in rows)
        {
            writer.WriteLine(FormatRow(row, widths));
        }

        WriteLegend(writer);
        WriteSummary(members, options, writer);
    }

    private static void WriteLegend(TextWriter writer)
    {
        writer.WriteLine();
        writer.WriteLine("Legend: Cog = Cognitive complexity; Cyc = Cyclomatic complexity; LOC = Lines of code; Nest = Max nesting depth; MI = Maintainability index; Member = member name; Location = File:Line.");
    }

    private static string FormatRow(IReadOnlyList<string> cells, IReadOnlyList<int> widths)
    {
        var builder = new StringBuilder();

        for (var column = 0; column < cells.Count; column++)
        {
            if (column > 0)
            {
                builder.Append("  ");
            }

            // Numbers right-aligned, text left-aligned.
            var isNumeric = column < 5;
            builder.Append(isNumeric
                ? cells[column].PadLeft(widths[column])
                : cells[column].PadRight(widths[column]));
        }

        return builder.ToString().TrimEnd();
    }

    private static void WriteSummary(IReadOnlyList<MemberMetrics> members, Options options, TextWriter writer)
    {
        var cognitive = members.Select(m => m.CognitiveComplexity).ToArray();
        var cyclomatic = members.Select(m => m.CyclomaticComplexity).ToArray();

        writer.WriteLine();
        // The averages are the only fractional numbers on this line, and an
        // interpolated string formats them with the ambient culture, which
        // would print "1,5" wherever the decimal separator is a comma.
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Cognitive    avg {Average(cognitive),5:F1}  median {Percentile(cognitive, 50),4}  p90 {Percentile(cognitive, 90),4}  max {cognitive.Max(),4}"));
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Cyclomatic   avg {Average(cyclomatic),5:F1}  median {Percentile(cyclomatic, 50),4}  p90 {Percentile(cyclomatic, 90),4}  max {cyclomatic.Max(),4}"));
        writer.WriteLine($"Total LOC    {members.Sum(m => m.LinesOfCode)}");
        writer.WriteLine();

        var simple = cognitive.Count(value => value <= 5);
        var moderate = cognitive.Count(value => value is > 5 and <= 10);
        var high = cognitive.Count(value => value is > 10 and <= 20);
        var severe = cognitive.Count(value => value > 20);

        writer.WriteLine($"Cognitive bands   simple(0-5) {simple}   moderate(6-10) {moderate}   high(11-20) {high}   severe(21+) {severe}");

        if (!options.HasGate)
        {
            return;
        }

        writer.WriteLine();

        if (options.MaxCognitive > 0)
        {
            var breaches = members.Count(m => m.CognitiveComplexity > options.MaxCognitive);
            writer.WriteLine($"Gate cognitive  <= {options.MaxCognitive}: {breaches} member(s) over the limit.");
        }

        if (options.MaxCyclomatic > 0)
        {
            var breaches = members.Count(m => m.CyclomaticComplexity > options.MaxCyclomatic);
            writer.WriteLine($"Gate cyclomatic <= {options.MaxCyclomatic}: {breaches} member(s) over the limit.");
        }
    }

    private static void WriteHtml(AnalysisReport report, string rootPath, TextWriter writer)
    {
        var json = JsonSerializer.Serialize(report.Members, JsonOptions)
            .Replace("</script>", "<\\/script>", StringComparison.OrdinalIgnoreCase);
        var sourceSummaryJson = JsonSerializer.Serialize(new
            {
                fileCount = report.SourceSummary.FileCount,
                lineCount = report.SourceSummary.LineCount,
                classCount = report.SourceSummary.ClassCount,
                recordCount = report.SourceSummary.RecordCount,
                enumCount = report.SourceSummary.EnumCount
            }, JsonOptions)
            .Replace("</script>", "<\\/script>", StringComparison.OrdinalIgnoreCase);

        var rootPathHtml = EscapeHtml(rootPath);

        writer.Write(
            $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1" />
              <title>Code Metrics Dashboard</title>
              <link rel="preconnect" href="https://fonts.googleapis.com" />
              <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
              <link href="https://fonts.googleapis.com/css2?family=Roboto:wght@400;500;700&family=Roboto+Mono&display=swap" rel="stylesheet" />
              <link href="https://fonts.googleapis.com/css2?family=Material+Symbols+Rounded:opsz,wght,FILL,GRAD@20..48,400..700,0..1,-50..200&display=block" rel="stylesheet" />
              <style>
                :root, [data-theme="light"] {
                  color-scheme: light;
                  --bg: #edf0f7;
                  --surface: #ffffff;
                  --surface-2: #f4f6fb;
                  --surface-3: #e9edf5;
                  --outline: rgba(16, 24, 40, 0.10);
                  --outline-strong: rgba(16, 24, 40, 0.20);
                  --text: #171c26;
                  --muted: #5c6572;
                  --primary: #4c6ef5;
                  --primary-tonal: rgba(76, 110, 245, 0.10);
                  --on-primary: #ffffff;
                  --chart-grid: rgba(16, 24, 40, 0.08);
                  --shadow-1: 0 1px 2px rgba(16, 24, 40, 0.06), 0 1px 3px rgba(16, 24, 40, 0.10);
                  --shadow-2: 0 8px 24px rgba(16, 24, 40, 0.10);
                  --risk-light-fg: #2b8a3e; --risk-light-bg: rgba(47, 158, 68, 0.12);
                  --risk-moderate-fg: #e67700; --risk-moderate-bg: rgba(230, 119, 0, 0.12);
                  --risk-high-fg: #d9480f; --risk-high-bg: rgba(217, 72, 15, 0.14);
                  --risk-severe-fg: #c92a2a; --risk-severe-bg: rgba(201, 42, 42, 0.13);
                  --a-blue: #4c6ef5; --a-orange: #e8590c; --a-violet: #7048e8; --a-green: #2f9e44; --a-red: #e03131;
                }

                [data-theme="dark"] {
                  color-scheme: dark;
                  --bg: #0b1220;
                  --surface: #121a2b;
                  --surface-2: #1a2438;
                  --surface-3: #232f48;
                  --outline: rgba(148, 163, 184, 0.14);
                  --outline-strong: rgba(148, 163, 184, 0.30);
                  --text: #e4e9f2;
                  --muted: #98a4b8;
                  --primary: #7c9aff;
                  --primary-tonal: rgba(124, 154, 255, 0.14);
                  --on-primary: #0b1220;
                  --chart-grid: rgba(148, 163, 184, 0.12);
                  --shadow-1: 0 1px 2px rgba(0, 0, 0, 0.45);
                  --shadow-2: 0 10px 28px rgba(0, 0, 0, 0.5);
                  --risk-light-fg: #69db7c; --risk-light-bg: rgba(105, 219, 124, 0.13);
                  --risk-moderate-fg: #ffd43b; --risk-moderate-bg: rgba(255, 212, 59, 0.13);
                  --risk-high-fg: #ff922b; --risk-high-bg: rgba(255, 146, 43, 0.15);
                  --risk-severe-fg: #ff8787; --risk-severe-bg: rgba(255, 135, 135, 0.15);
                  --a-blue: #74a9ff; --a-orange: #ff922b; --a-violet: #b197fc; --a-green: #69db7c; --a-red: #ff8787;
                }

                * { box-sizing: border-box; }

                html, body { margin: 0; }

                body {
                  font-family: "Roboto", "Segoe UI", system-ui, sans-serif;
                  background: var(--bg);
                  color: var(--text);
                  padding: 28px 20px 60px;
                  -webkit-font-smoothing: antialiased;
                }

                .shell {
                  max-width: 1400px;
                  margin: 0 auto;
                }

                .material-symbols-rounded {
                  font-family: "Material Symbols Rounded";
                  font-weight: normal;
                  font-style: normal;
                  font-size: 20px;
                  line-height: 1;
                  letter-spacing: normal;
                  text-transform: none;
                  display: inline-block;
                  white-space: nowrap;
                  word-wrap: normal;
                  direction: ltr;
                  font-variation-settings: "FILL" 0, "wght" 500, "GRAD" 0, "opsz" 24;
                }

                .topbar {
                  display: flex;
                  flex-wrap: wrap;
                  justify-content: space-between;
                  gap: 16px;
                  align-items: center;
                  margin-bottom: 22px;
                }

                .topbar-left {
                  display: flex;
                  align-items: center;
                  flex-wrap: wrap;
                  gap: 16px;
                  min-width: 0;
                }

                .brand { display: flex; align-items: center; gap: 14px; }

                .path-chip {
                  display: inline-flex;
                  align-items: center;
                  gap: 8px;
                  max-width: min(100%, 520px);
                  background: var(--surface);
                  border: 1px solid var(--outline);
                  border-radius: 12px;
                  padding: 9px 14px;
                  color: var(--muted);
                  box-shadow: var(--shadow-1);
                }

                .path-chip .material-symbols-rounded { color: var(--primary); font-size: 18px; }

                .path-chip .path-text {
                  overflow: hidden;
                  text-overflow: ellipsis;
                  white-space: nowrap;
                  font-family: "Roboto Mono", monospace;
                  font-size: 0.78rem;
                }

                .brand-icon {
                  width: 48px;
                  height: 48px;
                  display: grid;
                  place-items: center;
                  border-radius: 16px;
                  background: var(--primary-tonal);
                  color: var(--primary);
                }

                .brand-icon .material-symbols-rounded { font-size: 26px; }

                .eyebrow {
                  color: var(--primary);
                  font-weight: 600;
                  text-transform: uppercase;
                  letter-spacing: 0.1em;
                  font-size: 11px;
                }

                h1 {
                  margin: 2px 0 0;
                  font-size: clamp(1.5rem, 2.4vw, 2.1rem);
                  letter-spacing: -0.02em;
                  font-weight: 600;
                }

                .topbar-actions { display: flex; align-items: center; gap: 12px; }

                .stats-chip {
                  background: var(--primary-tonal);
                  border: 1px solid var(--outline);
                  color: var(--text);
                  padding: 8px 14px;
                  border-radius: 999px;
                  font-size: 0.85rem;
                  font-weight: 500;
                }

                .icon-button {
                  width: 40px;
                  height: 40px;
                  border-radius: 999px;
                  border: 1px solid var(--outline);
                  background: var(--surface);
                  color: var(--text);
                  display: grid;
                  place-items: center;
                  cursor: pointer;
                  box-shadow: var(--shadow-1);
                  transition: background-color 0.15s ease, transform 0.05s ease;
                }

                .icon-button:hover { background: var(--surface-2); }
                .icon-button:active { transform: scale(0.96); }

                :focus-visible { outline: 3px solid var(--primary); outline-offset: 2px; }

                .cards {
                  display: grid;
                  grid-template-columns: repeat(auto-fit, minmax(190px, 1fr));
                  gap: 14px;
                  margin-bottom: 16px;
                }

                .card {
                  background: var(--surface);
                  border: 1px solid var(--outline);
                  border-radius: 16px;
                  box-shadow: var(--shadow-1);
                  padding: 16px 18px;
                  position: relative;
                  overflow: hidden;
                }

                .card::before {
                  content: "";
                  position: absolute;
                  inset: 0 auto auto 0;
                  width: 100%;
                  height: 3px;
                  background: var(--card-accent, var(--primary));
                }

                .card-label {
                  display: block;
                  color: var(--muted);
                  font-size: 0.75rem;
                  letter-spacing: 0.05em;
                  text-transform: uppercase;
                  font-weight: 500;
                }

                .card-value {
                  display: block;
                  margin-top: 8px;
                  font-size: clamp(1.4rem, 1.8vw, 1.9rem);
                  letter-spacing: -0.02em;
                  font-weight: 700;
                  overflow-wrap: anywhere;
                }

                .card-note {
                  margin-top: 6px;
                  color: var(--muted);
                  font-size: 0.8rem;
                  overflow-wrap: anywhere;
                }

                .panel-grid {
                  display: grid;
                  grid-template-columns: repeat(2, minmax(320px, 1fr));
                  gap: 16px;
                  margin-bottom: 16px;
                }

                .panel {
                  background: var(--surface);
                  border: 1px solid var(--outline);
                  border-radius: 20px;
                  padding: 20px;
                  box-shadow: var(--shadow-1);
                }

                .table-panel + .panel { margin-top: 16px; }

                .panel h2 {
                  margin: 0 0 14px;
                  font-size: 0.95rem;
                  font-weight: 600;
                  letter-spacing: 0.01em;
                  display: flex;
                  align-items: center;
                  gap: 8px;
                }

                .panel h2 .material-symbols-rounded { color: var(--primary); font-size: 18px; }

                .chart-wrap {
                  position: relative;
                  height: 300px;
                }

                .table-panel {
                  margin-top: 16px;
                  background: var(--surface);
                  border: 1px solid var(--outline);
                  border-radius: 20px;
                  box-shadow: var(--shadow-1);
                  overflow: hidden;
                }

                .table-header {
                  display: flex;
                  flex-wrap: wrap;
                  justify-content: space-between;
                  gap: 12px;
                  padding: 18px 18px 10px;
                  align-items: center;
                }

                .table-header h2 {
                  margin: 0;
                  font-size: 0.95rem;
                  font-weight: 600;
                  display: flex;
                  align-items: center;
                  gap: 8px;
                }

                .table-header h2 .material-symbols-rounded { color: var(--primary); font-size: 18px; }

                .toolbar {
                  display: flex;
                  align-items: center;
                  flex-wrap: wrap;
                  gap: 10px;
                }

                .search-field {
                  position: relative;
                  display: flex;
                  align-items: center;
                }

                .search-field > .material-symbols-rounded {
                  position: absolute;
                  left: 12px;
                  color: var(--muted);
                  font-size: 18px;
                  pointer-events: none;
                }

                .toolbar input,
                .toolbar select {
                  appearance: none;
                  background: var(--surface-2);
                  border: 1px solid var(--outline-strong);
                  border-radius: 999px;
                  color: var(--text);
                  padding: 9px 14px;
                  min-height: 40px;
                  font-size: 0.9rem;
                }

                .toolbar input {
                  min-width: min(100%, 300px);
                  padding-left: 38px;
                }

                .toolbar input::placeholder { color: var(--muted); }

                .table-wrap {
                  overflow: auto;
                  max-height: 70vh;
                  padding: 0 18px 18px;
                }

                table {
                  width: 100%;
                  table-layout: fixed;
                  border-collapse: separate;
                  border-spacing: 0;
                  min-width: 1000px;
                }

                thead th:nth-child(1) { width: 230px; }
                thead th:nth-child(2) { width: 330px; }
                thead th:nth-child(3) { width: 90px; }
                thead th:nth-child(4) { width: 110px; }
                thead th:nth-child(5) { width: 80px; }
                thead th:nth-child(6) { width: 90px; }
                thead th:nth-child(7) { width: 80px; }
                thead th:nth-child(8) { width: 110px; }

                thead th {
                  position: sticky;
                  top: 0;
                  z-index: 1;
                  background: var(--surface);
                  text-align: left;
                  color: var(--muted);
                  font-size: 0.72rem;
                  letter-spacing: 0.07em;
                  text-transform: uppercase;
                  padding: 12px;
                  border-bottom: 1px solid var(--outline-strong);
                  white-space: nowrap;
                  overflow: hidden;
                  text-overflow: ellipsis;
                }

                th[data-sort] { cursor: pointer; user-select: none; }
                th[data-sort]:hover { color: var(--text); }

                .th-inner { display: inline-flex; align-items: center; gap: 4px; }
                .th-inner .material-symbols-rounded { font-size: 15px; }

                th { position: relative; }

                .resizer {
                  position: absolute;
                  top: 0;
                  right: 0;
                  width: 5px;
                  height: 100%;
                  cursor: col-resize;
                  user-select: none;
                  touch-action: none;
                  z-index: 2;
                }

                .resizer:hover,
                .resizer.resizing {
                  background: var(--primary);
                  opacity: 0.4;
                }

                thead th.num, tbody td.num { text-align: right; }

                tbody td {
                  padding: 10px 12px;
                  border-bottom: 1px solid var(--outline);
                  vertical-align: top;
                  font-size: 0.88rem;
                  overflow: hidden;
                }

                tbody td.num { font-family: "Roboto Mono", monospace; font-size: 0.82rem; }

                tbody tr:hover { background: var(--surface-2); }

                .member-name { font-weight: 500; word-break: break-word; }

                .member-type {
                  display: block;
                  color: var(--muted);
                  font-size: 0.75rem;
                  margin-top: 2px;
                }

                .file-name {
                  display: inline-flex;
                  align-items: center;
                  gap: 6px;
                  font-size: 0.85rem;
                  font-weight: 500;
                }

                .file-path {
                  display: block;
                  color: var(--muted);
                  font-size: 0.68rem;
                  margin-top: 2px;
                  font-family: "Roboto Mono", monospace;
                  word-break: break-all;
                  max-width: 100%;
                }

                .copy-btn {
                  display: inline-grid;
                  place-items: center;
                  width: 24px;
                  height: 24px;
                  padding: 0;
                  border-radius: 999px;
                  border: none;
                  background: transparent;
                  color: var(--muted);
                  cursor: pointer;
                  transition: background-color 0.15s ease, color 0.15s ease;
                }

                .copy-btn:hover { background: var(--primary-tonal); color: var(--primary); }
                .copy-btn .material-symbols-rounded { font-size: 15px; }
                .copy-btn.copied { color: var(--risk-light-fg); }

                .tag {
                  display: inline-flex;
                  align-items: center;
                  gap: 6px;
                  border-radius: 999px;
                  padding: 4px 10px;
                  font-size: 0.72rem;
                  font-weight: 600;
                  letter-spacing: 0.02em;
                  text-transform: uppercase;
                  white-space: nowrap;
                }

                .tag .dot { width: 6px; height: 6px; border-radius: 999px; background: currentColor; }

                .tag.light { background: var(--risk-light-bg); color: var(--risk-light-fg); }
                .tag.moderate { background: var(--risk-moderate-bg); color: var(--risk-moderate-fg); }
                .tag.high { background: var(--risk-high-bg); color: var(--risk-high-fg); }
                .tag.severe { background: var(--risk-severe-bg); color: var(--risk-severe-fg); }

                .empty-state {
                  color: var(--muted);
                  text-align: center;
                  padding: 38px 12px;
                }

                .table-foot {
                  display: flex;
                  justify-content: space-between;
                  align-items: center;
                  gap: 12px;
                  flex-wrap: wrap;
                  padding: 12px 18px 18px;
                  color: var(--muted);
                  font-size: 0.8rem;
                }

                .legend-intro {
                  margin: 0 0 14px;
                  color: var(--muted);
                  font-size: 0.85rem;
                }

                .legend-grid {
                  display: grid;
                  grid-template-columns: repeat(5, minmax(0, 1fr));
                  gap: 14px;
                }

                .legend-card {
                  background: var(--surface-2);
                  border: 1px solid var(--outline);
                  border-radius: 14px;
                  padding: 14px 16px;
                }

                .legend-card h3 {
                  margin: 0 0 6px;
                  font-size: 0.9rem;
                  display: flex;
                  align-items: center;
                  gap: 8px;
                }

                .legend-card h3 .material-symbols-rounded { font-size: 16px; color: var(--primary); }

                .legend-card p {
                  margin: 0 0 10px;
                  color: var(--muted);
                  font-size: 0.82rem;
                  line-height: 1.45;
                }

                .band-list { display: flex; flex-direction: column; gap: 6px; }

                .band-row {
                  display: flex;
                  align-items: center;
                  gap: 8px;
                  font-size: 0.78rem;
                }

                .band-row > .dot {
                  width: 10px;
                  height: 10px;
                  border-radius: 999px;
                  flex: none;
                }

                .band-row .range {
                  color: var(--muted);
                  margin-left: auto;
                  font-family: "Roboto Mono", monospace;
                  font-size: 0.72rem;
                  white-space: nowrap;
                }

                .toast {
                  position: fixed;
                  left: 50%;
                  bottom: 24px;
                  transform: translate(-50%, 20px);
                  background: var(--text);
                  color: var(--bg);
                  padding: 10px 18px;
                  border-radius: 8px;
                  box-shadow: var(--shadow-2);
                  font-size: 0.85rem;
                  font-weight: 500;
                  opacity: 0;
                  pointer-events: none;
                  transition: opacity 0.2s ease, transform 0.2s ease;
                  z-index: 50;
                }

                .toast.show { opacity: 1; transform: translate(-50%, 0); }

                @media (max-width: 1199px) {
                  .legend-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
                }

                @media (max-width: 980px) {
                  .panel-grid { grid-template-columns: 1fr; }
                }

                @media (max-width: 640px) {
                  .legend-grid { grid-template-columns: 1fr; }
                }

                @media (prefers-reduced-motion: reduce) {
                  * { transition: none !important; }
                }
              </style>
              <script>
                try {
                  const storedTheme = window.localStorage.getItem('codemetrics-theme');
                  const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
                  document.documentElement.dataset.theme = storedTheme || (prefersDark ? 'dark' : 'light');
                } catch {}
              </script>
            </head>
            <body>
              <div class="shell">
                <header class="topbar">
                  <div class="topbar-left">
                    <div class="brand">
                      <span class="brand-icon"><span class="material-symbols-rounded">monitoring</span></span>
                      <div>
                        <span class="eyebrow">Code quality</span>
                        <h1>Complexity dashboard</h1>
                      </div>
                    </div>
                    <div class="path-chip" title="{{rootPathHtml}}">
                      <span class="material-symbols-rounded">folder_open</span>
                      <span class="path-text">{{rootPathHtml}}</span>
                    </div>
                  </div>
                  <div class="topbar-actions">
                    <div class="stats-chip" id="statusChip" aria-live="polite">Ready</div>
                    <button id="themeToggle" class="icon-button" aria-label="Switch to dark mode" title="Toggle light/dark mode">
                      <span class="material-symbols-rounded" id="themeIcon">dark_mode</span>
                    </button>
                  </div>
                </header>

                <section id="summaryCards" class="cards" aria-live="polite"></section>
                <section id="sourceSummaryCards" class="cards" aria-live="polite"></section>

                <section class="panel-grid">
                  <article class="panel">
                    <h2><span class="material-symbols-rounded">bar_chart</span>Top cognitive complexity</h2>
                    <div class="chart-wrap"><canvas id="cognitiveChart" role="img" aria-label="Bar chart of the most cognitively complex members"></canvas></div>
                  </article>
                  <article class="panel">
                    <h2><span class="material-symbols-rounded">donut_small</span>Cyclomatic complexity</h2>
                    <div class="chart-wrap"><canvas id="cyclomaticChart" role="img" aria-label="Bar chart of the members with the highest cyclomatic complexity"></canvas></div>
                  </article>
                </section>

                <section class="panel-grid">
                  <article class="panel">
                    <h2><span class="material-symbols-rounded">build</span>Maintainability (lowest first)</h2>
                    <div class="chart-wrap"><canvas id="maintainabilityChart" role="img" aria-label="Horizontal bar chart of maintainability index, lowest values first"></canvas></div>
                  </article>
                  <article class="panel">
                    <h2><span class="material-symbols-rounded">pie_chart</span>Risk distribution</h2>
                    <div class="chart-wrap"><canvas id="riskChart" role="img" aria-label="Doughnut chart of members per risk band"></canvas></div>
                  </article>
                </section>

                <section class="table-panel">
                  <div class="table-header">
                    <h2>Member details</h2>
                    <div class="toolbar">
                      <div class="search-field">
                        <span class="material-symbols-rounded">search</span>
                        <input id="searchInput" type="search" placeholder="Search member, type, or file…" aria-label="Search by member, type, or file" />
                      </div>
                      <select id="sortSelect" aria-label="Sort members">
                        <option value="cognitive">Sort: cognitive</option>
                        <option value="cyclomatic">Sort: cyclomatic</option>
                        <option value="maintainability">Sort: maintainability</option>
                        <option value="nesting">Sort: nesting</option>
                        <option value="loc">Sort: LOC</option>
                        <option value="file">Sort: file</option>
                      </select>
                      <select id="limitSelect" aria-label="Display row count">
                        <option value="100">100 rows</option>
                        <option value="500">500 rows</option>
                        <option value="1000">1,000 rows</option>
                        <option value="3000" selected>3,000 rows</option>
                        <option value="3001">All</option>
                      </select>
                    </div>
                  </div>
                  <div class="table-wrap">
                    <table>
                      <thead>
                        <tr>
                          <th>Member<div class="resizer" data-col="0"></div></th>
                          <th>File<div class="resizer" data-col="1"></div></th>
                          <th class="num" data-sort="cognitive"><span class="th-inner">Cognitive<span><span class="material-symbols-rounded">unfold_more</span></span><div class="resizer" data-col="2"></div></th>
                          <th class="num" data-sort="cyclomatic"><span class="th-inner">Cyclomatic<span><span class="material-symbols-rounded">unfold_more</span></span><div class="resizer" data-col="3"></div></th>
                          <th class="num" data-sort="loc"><span class="th-inner">LOC<span><span class="material-symbols-rounded">unfold_more</span></span><div class="resizer" data-col="4"></div></th>
                          <th class="num" data-sort="nesting"><span class="th-inner">Nesting<span><span class="material-symbols-rounded">unfold_more</span></span><div class="resizer" data-col="5"></div></th>
                          <th class="num" data-sort="maintainability"><span class="th-inner">MI<span><span class="material-symbols-rounded">unfold_more</span></span><div class="resizer" data-col="6"></div></th>
                          <th>Risk<div class="resizer" data-col="7"></div></th>
                        </tr>
                      </thead>
                      <tbody id="membersTable"></tbody>
                    </table>
                  </div>
                  <div class="table-foot">
                    <span id="rowCount">0 members shown</span>
                    <span>Click a column header to sort · click again to reverse</span>
                  </div>
                </section>

                <section class="panel" aria-label="Metric definitions">
                  <h2><span class="material-symbols-rounded">info</span>How to read the metrics</h2>
                  <p class="legend-intro">Each metric is scored per member (method, constructor, accessor or expression-bodied member). The bands below use the same colour language across the whole report.</p>
                  <div class="legend-grid" id="legendGrid"></div>
                </section>
              </div>
              <div id="toast" class="toast" role="status" aria-live="polite"></div>

              <script type="application/json" id="metrics-data">{{json}}</script>
              <script type="application/json" id="source-summary-data">{{sourceSummaryJson}}</script>
              <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.3/dist/chart.umd.min.js"></script>
              <script>
                const metrics = JSON.parse(document.getElementById('metrics-data').textContent || '[]');
                const sourceSummary = JSON.parse(document.getElementById('source-summary-data').textContent || '{}');

                /* ---------- Theme ---------- */
                const themeToggle = document.getElementById('themeToggle');
                const themeIcon = document.getElementById('themeIcon');

                const applyThemeMeta = () => {
                  const isDark = document.documentElement.dataset.theme === 'dark';
                  themeIcon.textContent = isDark ? 'light_mode' : 'dark_mode';
                  themeToggle.setAttribute('aria-label', isDark ? 'Switch to light mode' : 'Switch to dark mode');
                };

                themeToggle.addEventListener('click', () => {
                  const next = document.documentElement.dataset.theme === 'dark' ? 'light' : 'dark';
                  document.documentElement.dataset.theme = next;
                  try { window.localStorage.setItem('codemetrics-theme', next); } catch {}
                  applyThemeMeta();
                  renderCharts();
                });

                applyThemeMeta();

                /* ---------- Helpers ---------- */
                const formatNumber = (value) => new Intl.NumberFormat('en-US', { maximumFractionDigits: 1 }).format(value);
                const formatInteger = (value) => new Intl.NumberFormat('en-US').format(value);
                const clamp = (value, min, max) => Math.min(Math.max(value, min), max);

                const asNumber = (value, fallback = 0) => {
                  const numeric = Number(value);
                  return Number.isFinite(numeric) ? numeric : fallback;
                };

                const escapeHtml = (value) => String(value ?? '')
                  .replaceAll('&', '&amp;')
                  .replaceAll('<', '&lt;')
                  .replaceAll('>', '&gt;')
                  .replaceAll('"', '&quot;')
                  .replaceAll("'", '&#39;');

                const truncate = (value, max = 26) => value.length > max ? `${value.slice(0, max - 1)}…` : value;

                const fileNameOf = (filePath) => {
                  const normalized = String(filePath || '').replace(/\\/g, '/');
                  return normalized.split('/').pop() || normalized;
                };

                const cssVar = (name) => getComputedStyle(document.documentElement).getPropertyValue(name).trim();

                const getRiskBand = (value) => {
                  if (value > 20) return { label: 'Severe', className: 'severe', color: () => cssVar('--risk-severe-fg') };
                  if (value > 10) return { label: 'High', className: 'high', color: () => cssVar('--risk-high-fg') };
                  if (value > 5) return { label: 'Moderate', className: 'moderate', color: () => cssVar('--risk-moderate-fg') };
                  return { label: 'Light', className: 'light', color: () => cssVar('--risk-light-fg') };
                };

                /* ---------- Summary cards ---------- */
                const totalMembers = metrics.length;
                const avgCognitive = totalMembers > 0 ? metrics.reduce((sum, item) => sum + asNumber(item.cognitiveComplexity), 0) / totalMembers : 0;
                const avgCyclomatic = totalMembers > 0 ? metrics.reduce((sum, item) => sum + asNumber(item.cyclomaticComplexity), 0) / totalMembers : 0;
                const avgMaintainability = totalMembers > 0 ? metrics.reduce((sum, item) => sum + asNumber(item.maintainabilityIndex), 0) / totalMembers : 0;
                const topRisk = totalMembers > 0 ? [...metrics].sort((a, b) => asNumber(b.cognitiveComplexity) - asNumber(a.cognitiveComplexity))[0] : null;

                const summaryCards = [
                  { label: 'Members', value: formatInteger(totalMembers), note: 'Analysed members', accent: 'var(--a-blue)' },
                  { label: 'Avg cognitive', value: formatNumber(avgCognitive), note: 'Effort of understanding', accent: 'var(--a-orange)' },
                  { label: 'Avg cyclomatic', value: formatNumber(avgCyclomatic), note: 'Independent paths', accent: 'var(--a-violet)' },
                  { label: 'Avg MI', value: formatNumber(avgMaintainability), note: 'Maintainability index', accent: 'var(--a-green)' },
                  { label: 'Highest risk', value: topRisk ? truncate(topRisk.memberName, 22) : 'n/a', note: topRisk ? `${escapeHtml(truncate(topRisk.typeName, 40))} · ${escapeHtml(fileNameOf(topRisk.filePath))}` : 'No member data', accent: 'var(--a-red)' }
                ];

                const sourceSummaryCards = [
                  { label: 'Total C# files', value: formatInteger(asNumber(sourceSummary.fileCount)), note: 'Included source files', accent: 'var(--a-blue)' },
                  { label: 'Total lines', value: formatInteger(asNumber(sourceSummary.lineCount)), note: 'Physical file lines', accent: 'var(--a-orange)' },
                  { label: 'C# classes', value: formatInteger(asNumber(sourceSummary.classCount)), note: 'All class declarations', accent: 'var(--a-violet)' },
                  { label: 'C# records', value: formatInteger(asNumber(sourceSummary.recordCount)), note: 'Class and struct records', accent: 'var(--a-green)' },
                  { label: 'C# enums', value: formatInteger(asNumber(sourceSummary.enumCount)), note: 'Enum declarations', accent: 'var(--a-red)' }
                ];

                const summaryRoot = document.getElementById('summaryCards');
                summaryRoot.innerHTML = summaryCards.map((card) => `
                  <article class="card" style="--card-accent: ${card.accent};">
                    <span class="card-label">${escapeHtml(card.label)}</span>
                    <span class="card-value">${escapeHtml(card.value)}</span>
                    <div class="card-note">${card.note}</div>
                  </article>
                `).join('');

                const sourceSummaryRoot = document.getElementById('sourceSummaryCards');
                sourceSummaryRoot.innerHTML = sourceSummaryCards.map((card) => `
                  <article class="card" style="--card-accent: ${card.accent};">
                    <span class="card-label">${escapeHtml(card.label)}</span>
                    <span class="card-value">${escapeHtml(card.value)}</span>
                    <div class="card-note">${card.note}</div>
                  </article>
                `).join('');

                /* ---------- Toast ---------- */
                const toastElement = document.getElementById('toast');
                let toastTimer = 0;
                const showToast = (message) => {
                  toastElement.textContent = message;
                  toastElement.classList.add('show');
                  window.clearTimeout(toastTimer);
                  toastTimer = window.setTimeout(() => toastElement.classList.remove('show'), 1800);
                };

                /* ---------- Charts ---------- */
                const charts = {};

                const chartTheme = () => ({
                  text: cssVar('--muted') || '#888',
                  grid: cssVar('--chart-grid') || 'rgba(128,128,128,0.12)'
                });

                const baseBarOptions = (titleAxis = 'x') => {
                  const theme = chartTheme();
                  const opposite = titleAxis === 'x' ? 'y' : 'x';
                  return {
                    responsive: true,
                    maintainAspectRatio: false,
                    animation: { duration: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 0 : 450 },
                    plugins: {
                      legend: { display: false },
                      tooltip: {
                        backgroundColor: cssVar('--surface-3') || '#222',
                        titleColor: cssVar('--text') || '#fff',
                        bodyColor: cssVar('--muted') || '#ccc',
                        padding: 10,
                        cornerRadius: 8
                      }
                    },
                    scales: {
                      [titleAxis]: { ticks: { color: theme.text, maxRotation: 45, minRotation: titleAxis === 'x' ? 20 : 0 }, grid: { display: false } },
                      [opposite]: { beginAtZero: true, ticks: { color: theme.text }, grid: { color: theme.grid } }
                    }
                  };
                };

                const renderCharts = () => {
                  Object.values(charts).forEach((chart) => chart.destroy());
                  if (typeof Chart === 'undefined') return;

                  const top = (selector, count) => [...metrics]
                    .sort((a, b) => asNumber(b[selector]) - asNumber(a[selector]))
                    .slice(0, count)
                    .map((item) => ({
                      label: truncate(item.memberName, 22),
                      fullName: `${item.typeName}.${item.memberName}`,
                      file: fileNameOf(item.filePath),
                      value: asNumber(item[selector])
                    }));

                  const withMemberTooltip = (options) => ({
                    ...options,
                    plugins: {
                      ...options.plugins,
                      tooltip: {
                        ...options.plugins.tooltip,
                        callbacks: {
                          title: (items) => {
                            const item = items[0];
                            const source = item.chart.data.members?.[item.dataIndex];
                            return source ? source.fullName : item.label;
                          },
                          afterTitle: (items) => {
                            const item = items[0];
                            const source = item.chart.data.members?.[item.dataIndex];
                            return source ? source.file : '';
                          }
                        }
                      }
                    }
                  });

                  const cognitive = top('cognitiveComplexity', 8);
                  charts.cognitive = new Chart(document.getElementById('cognitiveChart'), {
                    type: 'bar',
                    data: {
                      labels: cognitive.map((item) => item.label),
                      members: cognitive,
                      datasets: [{
                        label: 'Cognitive complexity',
                        data: cognitive.map((item) => item.value),
                        backgroundColor: cognitive.map((item) => getRiskBand(item.value).color()),
                        borderRadius: 8,
                        maxBarThickness: 34
                      }]
                    },
                    options: withMemberTooltip(baseBarOptions())
                  });

                  const cyclomatic = top('cyclomaticComplexity', 8);
                  charts.cyclomatic = new Chart(document.getElementById('cyclomaticChart'), {
                    type: 'bar',
                    data: {
                      labels: cyclomatic.map((item) => item.label),
                      members: cyclomatic,
                      datasets: [{
                        label: 'Cyclomatic complexity',
                        data: cyclomatic.map((item) => item.value),
                        backgroundColor: cssVar('--a-violet'),
                        borderRadius: 8,
                        maxBarThickness: 34
                      }]
                    },
                    options: withMemberTooltip(baseBarOptions())
                  });

                  const lowestMaintainability = [...metrics]
                    .sort((a, b) => asNumber(a.maintainabilityIndex) - asNumber(b.maintainabilityIndex))
                    .slice(0, 8)
                    .map((item) => ({
                      label: truncate(item.memberName, 22),
                      fullName: `${item.typeName}.${item.memberName}`,
                      file: fileNameOf(item.filePath),
                      value: asNumber(item.maintainabilityIndex)
                    }));

                  charts.maintainability = new Chart(document.getElementById('maintainabilityChart'), {
                    type: 'bar',
                    data: {
                      labels: lowestMaintainability.map((item) => item.label),
                      members: lowestMaintainability,
                      datasets: [{
                        label: 'Maintainability index',
                        data: lowestMaintainability.map((item) => clamp(item.value, 0, 100)),
                        backgroundColor: lowestMaintainability.map((item) => item.value >= 65 ? cssVar('--risk-light-fg') : item.value >= 30 ? cssVar('--risk-moderate-fg') : cssVar('--risk-severe-fg')),
                        borderRadius: 6,
                        maxBarThickness: 20
                      }]
                    },
                    options: (() => {
                      const options = baseBarOptions('y');
                      options.scales.x = { min: 0, max: 100, ticks: { color: chartTheme().text }, grid: { color: chartTheme().grid } };
                      return withMemberTooltip(options);
                    })()
                  });

                  const riskBands = { Light: 0, Moderate: 0, High: 0, Severe: 0 };
                  metrics.forEach((member) => {
                    riskBands[getRiskBand(asNumber(member.cognitiveComplexity)).label] += 1;
                  });

                  charts.risk = new Chart(document.getElementById('riskChart'), {
                    type: 'doughnut',
                    data: {
                      labels: Object.keys(riskBands),
                      datasets: [{
                        data: Object.values(riskBands),
                        backgroundColor: [
                          cssVar('--risk-light-fg'),
                          cssVar('--risk-moderate-fg'),
                          cssVar('--risk-high-fg'),
                          cssVar('--risk-severe-fg')
                        ],
                        borderWidth: 0
                      }]
                    },
                    options: {
                      responsive: true,
                      maintainAspectRatio: false,
                      cutout: '62%',
                      animation: { duration: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 0 : 450 },
                      plugins: {
                        legend: {
                          position: 'bottom',
                          labels: { color: cssVar('--text'), usePointStyle: true, pointStyle: 'circle', padding: 16 }
                        },
                        tooltip: { callbacks: {
                          label: (item) => ` ${item.label}: ${item.formattedValue} member(s)`
                        } }
                      }
                    }
                  });
                };

                /* ---------- Table ---------- */
                const ALL_ROWS = 3001;
                const defaultDirection = { cognitive: 'desc', cyclomatic: 'desc', maintainability: 'asc', loc: 'desc', nesting: 'desc', file: 'asc' };
                const sortState = { key: 'cognitive', dir: 'desc' };

                const metricOf = (member, key) => {
                  switch (key) {
                    case 'cyclomatic': return asNumber(member.cyclomaticComplexity);
                    case 'maintainability': return asNumber(member.maintainabilityIndex);
                    case 'loc': return asNumber(member.linesOfCode);
                    case 'nesting': return asNumber(member.maxNestingDepth);
                    case 'file': return 0;
                    default: return asNumber(member.cognitiveComplexity);
                  }
                };

                const renderTable = () => {
                  const searchInput = document.getElementById('searchInput');
                  const limitSelect = document.getElementById('limitSelect');
                  const tableBody = document.getElementById('membersTable');

                  const query = searchInput.value.trim().toLowerCase();
                  const limit = Number(limitSelect.value || ALL_ROWS);

                  let rows = metrics.filter((member) => {
                    const haystack = [member.memberName, member.typeName, member.filePath, member.fullName].join(' ').toLowerCase();
                    return haystack.includes(query);
                  });

                  rows.sort((a, b) => {
                    const multiplier = sortState.dir === 'asc' ? 1 : -1;

                    if (sortState.key === 'file') {
                      return multiplier * ((a.filePath || '').localeCompare(b.filePath || '') || (a.memberName || '').localeCompare(b.memberName || ''));
                    }

                    return multiplier * (metricOf(a, sortState.key) - metricOf(b, sortState.key));
                  });

                  document.querySelectorAll('th[data-sort]').forEach((header) => {
                    const icon = header.querySelector('.th-inner .material-symbols-rounded');
                    if (!icon) return;

                    if (header.dataset.sort === sortState.key) {
                      icon.textContent = sortState.dir === 'asc' ? 'expand_less' : 'expand_more';
                      header.setAttribute('aria-sort', sortState.dir === 'asc' ? 'ascending' : 'descending');
                    } else {
                      icon.textContent = 'unfold_more';
                      header.removeAttribute('aria-sort');
                    }
                  });

                  document.getElementById('sortSelect').value = sortState.key;

                  const visibleRows = limit === ALL_ROWS ? rows : rows.slice(0, limit);
                  tableBody.innerHTML = visibleRows.length === 0
                    ? '<tr><td colspan="8" class="empty-state">No members match the current filter.</td></tr>'
                    : visibleRows.map((member) => {
                        const risk = getRiskBand(asNumber(member.cognitiveComplexity));
                        return `
                          <tr>
                            <td>
                              <div class="member-name">${escapeHtml(member.memberName)}</div>
                              <span class="member-type">${escapeHtml(member.typeName)}</span>
                            </td>
                            <td>
                              <span class="file-name">
                                ${escapeHtml(fileNameOf(member.filePath))}
                                <button class="copy-btn" data-copy="${escapeHtml(fileNameOf(member.filePath))}" aria-label="Copy file name ${escapeHtml(fileNameOf(member.filePath))}" title="Copy file name">
                                  <span class="material-symbols-rounded">content_copy</span>
                                </button>
                              </span>
                              <span class="file-path">${escapeHtml(member.filePath)} · line ${asNumber(member.lineNumber)}</span>
                            </td>
                            <td class="num">${asNumber(member.cognitiveComplexity)}</td>
                            <td class="num">${asNumber(member.cyclomaticComplexity)}</td>
                            <td class="num">${asNumber(member.linesOfCode)}</td>
                            <td class="num">${asNumber(member.maxNestingDepth)}</td>
                            <td class="num">${formatNumber(asNumber(member.maintainabilityIndex))}</td>
                            <td><span class="tag ${risk.className}"><span class="dot"></span>${risk.label}</span></td>
                          </tr>
                        `;
                      }).join('');

                  const totalLabel = `${formatInteger(visibleRows.length)} of ${formatInteger(rows.length)} members${query ? ' (filtered)' : ''}`;
                  document.getElementById('rowCount').textContent = totalLabel;
                  document.getElementById('statusChip').textContent = query ? 'Filtered results' : 'Ready';
                };

                document.getElementById('searchInput').addEventListener('input', renderTable);

                document.getElementById('sortSelect').addEventListener('change', (event) => {
                  sortState.key = event.target.value;
                  sortState.dir = defaultDirection[sortState.key] || 'desc';
                  renderTable();
                });

                document.getElementById('limitSelect').addEventListener('change', renderTable);

                document.querySelectorAll('th[data-sort]').forEach((header) => {
                  header.addEventListener('click', () => {
                    const key = header.dataset.sort;
                    if (sortState.key === key) {
                      sortState.dir = sortState.dir === 'asc' ? 'desc' : 'asc';
                    } else {
                      sortState.key = key;
                      sortState.dir = defaultDirection[key] || 'desc';
                    }
                    renderTable();
                  });

                  header.addEventListener('keydown', (event) => {
                    if (event.key === 'Enter' || event.key === ' ') {
                      event.preventDefault();
                      header.click();
                    }
                  });

                  header.setAttribute('tabindex', '0');
                });

                /* ---------- Column resize ---------- */
                const table = document.querySelector('.table-wrap table');

                document.querySelectorAll('.resizer').forEach((resizer) => {
                  let startX = 0;
                  let startWidth = 0;
                  let colIndex = 0;
                  let pinnedTotal = 0;

                  const onMouseDown = (event) => {
                    event.preventDefault();
                    event.stopPropagation();
                    colIndex = Number(resizer.dataset.col);
                    startX = event.clientX;

                    // Pin every column to its rendered width so the fixed
                    // layout honours exactly what we set while dragging.
                    const cells = [...table.tHead.rows[0].cells];
                    const widths = cells.map((cell) => cell.getBoundingClientRect().width);
                    widths.forEach((width, index) => { cells[index].style.width = `${width}px`; });
                    pinnedTotal = widths.reduce((sum, width) => sum + width, 0);
                    startWidth = widths[colIndex];
                    table.style.width = `${pinnedTotal}px`;

                    resizer.classList.add('resizing');
                    document.addEventListener('mousemove', onMouseMove);
                    document.addEventListener('mouseup', onMouseUp);
                    document.body.style.cursor = 'col-resize';
                    document.body.style.userSelect = 'none';
                  };

                  const onMouseMove = (event) => {
                    const newWidth = Math.max(40, startWidth + (event.clientX - startX));
                    table.tHead.rows[0].cells[colIndex].style.width = `${newWidth}px`;
                    table.style.width = `${pinnedTotal - startWidth + newWidth}px`;
                  };

                  const onMouseUp = () => {
                    resizer.classList.remove('resizing');
                    document.removeEventListener('mousemove', onMouseMove);
                    document.removeEventListener('mouseup', onMouseUp);
                    document.body.style.cursor = '';
                    document.body.style.userSelect = '';
                  };

                  resizer.addEventListener('mousedown', onMouseDown);
                });

                /* ---------- Copy file name ---------- */
                const fallbackCopy = (value) => {
                  const textarea = document.createElement('textarea');
                  textarea.value = value;
                  textarea.style.position = 'fixed';
                  textarea.style.opacity = '0';
                  document.body.appendChild(textarea);
                  textarea.select();
                  try { document.execCommand('copy'); showToast('File name copied to clipboard'); }
                  catch { showToast('Copy failed — select the name manually'); }
                  textarea.remove();
                };

                document.getElementById('membersTable').addEventListener('click', async (event) => {
                  const button = event.target.closest('.copy-btn');
                  if (!button) return;

                  const value = button.dataset.copy || '';
                  try {
                    await navigator.clipboard.writeText(value);
                    showToast('File name copied to clipboard');
                  } catch {
                    fallbackCopy(value);
                  }

                  button.classList.add('copied');
                  window.setTimeout(() => button.classList.remove('copied'), 1500);
                });

                /* ---------- Legend ---------- */
                const legend = [
                  {
                    icon: 'psychology',
                    title: 'Cognitive complexity',
                    description: 'How hard the code is for a human to understand. Increments for each break in linear flow (if, loop, switch, catch, &&/||, ternary) and an extra penalty per nesting level. Lower is better.',
                    bands: [
                      { label: 'Light', range: '0–5', color: '--risk-light-fg' },
                      { label: 'Moderate', range: '6–10', color: '--risk-moderate-fg' },
                      { label: 'High', range: '11–20', color: '--risk-high-fg' },
                      { label: 'Severe', range: '21+', color: '--risk-severe-fg' }
                    ]
                  },
                  {
                    icon: 'share',
                    title: 'Cyclomatic complexity',
                    description: 'The number of independent execution paths through the code. Starts at 1 and adds one per decision point: if, loop, case, switch arm, catch, when, ternary, ?., and each &&, ||, ??, ??=, and/or pattern. A rough proxy for the number of tests required. Lower is better.',
                    bands: [
                      { label: 'Light', range: '1–10', color: '--risk-light-fg' },
                      { label: 'Moderate', range: '11–20', color: '--risk-moderate-fg' },
                      { label: 'High', range: '21–50', color: '--risk-high-fg' },
                      { label: 'Severe', range: '50+', color: '--risk-severe-fg' }
                    ]
                  },
                  {
                    icon: 'build',
                    title: 'Maintainability index (MI)',
                    description: 'Visual Studio-style composite score derived from Halstead volume, cyclomatic complexity and lines of code, normalised to 0 (worst) – 100 (best). Higher is better.',
                    bands: [
                      { label: 'Good', range: '65–100', color: '--risk-light-fg' },
                      { label: 'Moderate', range: '30–64', color: '--risk-moderate-fg' },
                      { label: 'Low', range: '0–29', color: '--risk-severe-fg' }
                    ]
                  },
                  {
                    icon: 'format_list_numbered',
                    title: 'LOC & nesting',
                    description: 'LOC is the physical number of lines in the member body. Nesting is the deepest level of nested control flow (loops, ifs, try blocks). Both are soft signals: large or deeply nested bodies are harder to review. Consider refactoring above roughly 60 LOC or nesting depth 4.',
                    bands: [
                      { label: 'Comfortable', range: 'nesting ≤ 3', color: '--risk-light-fg' },
                      { label: 'Watch', range: 'nesting 4–5', color: '--risk-moderate-fg' },
                      { label: 'Deep', range: 'nesting 6+', color: '--risk-severe-fg' }
                    ]
                  },
                  {
                    icon: 'flag',
                    title: 'Risk bands',
                    description: 'The risk tag in the table is derived from cognitive complexity. The same four colours are reused in the chart bars and the doughnut, so severity is scannable at a glance.',
                    bands: [
                      { label: 'Light', range: 'cognitive ≤ 5', color: '--risk-light-fg' },
                      { label: 'Moderate', range: '6–10', color: '--risk-moderate-fg' },
                      { label: 'High', range: '11–20', color: '--risk-high-fg' },
                      { label: 'Severe', range: '21+', color: '--risk-severe-fg' }
                    ]
                  }
                ];

                document.getElementById('legendGrid').innerHTML = legend.map((card) => `
                  <article class="legend-card">
                    <h3><span class="material-symbols-rounded">${card.icon}</span>${escapeHtml(card.title)}</h3>
                    <p>${escapeHtml(card.description)}</p>
                    <div class="band-list">
                      ${card.bands.map((band) => `
                        <div class="band-row">
                          <span class="dot" style="background: var(${band.color});"></span>
                          <span>${escapeHtml(band.label)}</span>
                          <span class="range">${escapeHtml(band.range)}</span>
                        </div>
                      `).join('')}
                    </div>
                  </article>
                `).join('');

                renderCharts();
                renderTable();
              </script>
            </body>
            </html>
            """
        );
    }

    private static void WriteCsv(IReadOnlyList<MemberMetrics> members, TextWriter writer)
    {
        writer.WriteLine("File,Line,Type,Member,Cognitive,Cyclomatic,LinesOfCode,MaxNesting,Parameters,MaintainabilityIndex");

        foreach (var member in members.OrderByDescending(m => m.CognitiveComplexity))
        {
            writer.WriteLine(string.Join(',',
                Escape(member.FilePath),
                member.LineNumber,
                Escape(member.TypeName),
                Escape(member.MemberName),
                member.CognitiveComplexity,
                member.CyclomaticComplexity,
                member.LinesOfCode,
                member.MaxNestingDepth,
                member.ParameterCount,
                member.MaintainabilityIndex.ToString("F1", CultureInfo.InvariantCulture)));
        }
    }

    private static string Escape(string value) =>
        value.Contains(',') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;

    private static string EscapeHtml(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : string.Concat("...", value.AsSpan(value.Length - maxLength + 3));

    private static double Average(IReadOnlyCollection<int> values) =>
        values.Count == 0 ? 0d : values.Average();

    /// <summary>Nearest-rank percentile. Good enough for a report, no interpolation.</summary>
    private static int Percentile(int[] values, int percentile)
    {
        if (values.Length == 0)
        {
            return 0;
        }

        var sorted = values.Order().ToArray();
        var rank = (int)Math.Ceiling(percentile / 100d * sorted.Length);

        return sorted[Math.Clamp(rank - 1, 0, sorted.Length - 1)];
    }
}
