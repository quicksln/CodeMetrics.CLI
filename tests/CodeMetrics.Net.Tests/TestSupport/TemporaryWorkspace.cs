namespace CodeMetrics.Net.Tests.TestSupport;

/// <summary>
/// A throwaway directory tree. <c>SourceFileLocator</c> globs the real file
/// system, so its rules can only be pinned against real files.
/// </summary>
internal sealed class TemporaryWorkspace : IDisposable
{
    public TemporaryWorkspace()
    {
        Root = Path.Combine(Path.GetTempPath(), $"codemetrics-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    /// <summary>
    /// Creates a file at a path relative to the workspace root, making any
    /// intermediate directories. Returns the full path.
    /// </summary>
    public string AddFile(string relativePath, string content = "class C { }")
    {
        var fullPath = Path.GetFullPath(Path.Combine(Root, relativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);

        return fullPath;
    }

    public string Combine(string relativePath) => Path.GetFullPath(Path.Combine(Root, relativePath));

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A leaked temp directory must never fail a test run.
        }
    }
}
