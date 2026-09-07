using System.Text.RegularExpressions;

namespace CodeMetrics.Analysis;

/// <summary>
/// Turns whatever the user passed on the command line into a list of .cs files.
/// </summary>
/// <remarks>
/// This globs the file system instead of asking MSBuild. That means it never
/// needs a restore, never needs the target project's SDK installed, and runs in
/// milliseconds. The trade-off is that it ignores <c>Compile Remove</c> items
/// and linked files, so a project with unusual item groups may report a few
/// files MSBuild would not compile.
/// </remarks>
public static partial class SourceFileLocator
{
    private static readonly string[] GeneratedSuffixes =
    [
        ".g.cs",
        ".g.i.cs",
        ".designer.cs",
        ".generated.cs",
        "assemblyinfo.cs",
        "assemblyattributes.cs"
    ];

    public static IReadOnlyList<string> Locate(string path)
    {
        if (File.Exists(path))
        {
            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".cs" => [Path.GetFullPath(path)],
                ".csproj" => EnumerateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!),
                ".sln" or ".slnx" => FromSolution(path),
                _ => []
            };
        }

        return Directory.Exists(path) ? EnumerateDirectory(path) : [];
    }

    private static IReadOnlyList<string> FromSolution(string solutionPath)
    {
        var fullPath = Path.GetFullPath(solutionPath);
        var solutionDirectory = Path.GetDirectoryName(fullPath)!;
        var content = File.ReadAllText(fullPath);

        var projectDirectories = ProjectPathPattern()
            .Matches(content)
            .Select(match => match.Value.Replace('\\', Path.DirectorySeparatorChar))
            .Select(relative => Path.GetFullPath(Path.Combine(solutionDirectory, relative)))
            .Where(File.Exists)
            .Select(projectFile => Path.GetDirectoryName(projectFile)!)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return projectDirectories
            .SelectMany(EnumerateDirectory)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> EnumerateDirectory(string directory) =>
        Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(IsAnalyzable)
            .Select(Path.GetFullPath)
            .ToList();

    private static bool IsAnalyzable(string filePath)
    {
        var normalized = filePath.Replace('\\', '/');

        if (normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !GeneratedSuffixes.Any(suffix => normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Pulls project paths out of both the legacy .sln format and the newer
    /// XML .slnx format by matching anything that ends in .csproj.
    /// </summary>
    [GeneratedRegex("""[^"<>|*?\r\n]+\.csproj""", RegexOptions.IgnoreCase)]
    private static partial Regex ProjectPathPattern();
}
