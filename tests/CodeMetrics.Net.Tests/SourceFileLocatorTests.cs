using CodeMetrics.Analysis;
using CodeMetrics.Net.Tests.TestSupport;
using Xunit;

namespace CodeMetrics.Net.Tests;

/// <summary>
/// Source discovery decides what the tool measures, so the exclusion rules are
/// as much a contract as the metrics themselves: a build output or a generated
/// file counted as hand-written code would distort every number in the report.
/// </summary>
public class SourceFileLocatorTests
{
    [Fact]
    public void CsFile_ResolvesToItselfAsAFullPath()
    {
        using var workspace = new TemporaryWorkspace();
        var file = workspace.AddFile("Widget.cs");

        var located = SourceFileLocator.Locate(file);

        Assert.Equal([file], located);
    }

    [Fact]
    public void Directory_IsSearchedRecursively()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.AddFile("Widget.cs");
        workspace.AddFile("Nested/Deep/Gadget.cs");

        Assert.Equal(["Gadget.cs", "Widget.cs"], NamesOf(SourceFileLocator.Locate(workspace.Root)));
    }

    [Theory]
    // Build output is not source.
    [InlineData("bin/Debug/net10.0/Widget.cs")]
    [InlineData("obj/Debug/Widget.cs")]
    [InlineData("Nested/bin/Widget.cs")]
    [InlineData("BIN/Widget.cs")]
    // Generated code is not hand-written, so counting it would distort the report.
    [InlineData("Widget.g.cs")]
    [InlineData("Widget.g.i.cs")]
    [InlineData("Widget.Designer.cs")]
    [InlineData("Widget.generated.cs")]
    [InlineData("Properties/AssemblyInfo.cs")]
    [InlineData("Properties/AssemblyAttributes.cs")]
    public void GeneratedAndBuildOutput_IsExcluded(string relativePath)
    {
        using var workspace = new TemporaryWorkspace();
        workspace.AddFile("Kept.cs");
        workspace.AddFile(relativePath);

        Assert.Equal(["Kept.cs"], NamesOf(SourceFileLocator.Locate(workspace.Root)));
    }

    [Fact]
    public void ProjectFile_SearchesItsOwnDirectory()
    {
        using var workspace = new TemporaryWorkspace();
        var project = workspace.AddFile("App/App.csproj", "<Project />");
        workspace.AddFile("App/Widget.cs");
        workspace.AddFile("App/Nested/Gadget.cs");
        workspace.AddFile("Other/Outside.cs");

        Assert.Equal(["Gadget.cs", "Widget.cs"], NamesOf(SourceFileLocator.Locate(project)));
    }

    [Fact]
    public void Solution_SearchesEveryProjectItReferences()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.AddFile("App/App.csproj", "<Project />");
        workspace.AddFile("App/Widget.cs");
        workspace.AddFile("Lib/Lib.csproj", "<Project />");
        workspace.AddFile("Lib/Gadget.cs");
        workspace.AddFile("Unreferenced/Ignored.cs");

        // Backslash separators, the way a real .sln writes them.
        var solution = workspace.AddFile(
            "App.sln",
            """
            Project("{FAE04EC0}") = "App", "App\App.csproj", "{111}"
            EndProject
            Project("{FAE04EC0}") = "Lib", "Lib\Lib.csproj", "{222}"
            EndProject
            """);

        Assert.Equal(["Gadget.cs", "Widget.cs"], NamesOf(SourceFileLocator.Locate(solution)));
    }

    [Fact]
    public void SlnxSolution_IsReadTheSameWay()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.AddFile("App/App.csproj", "<Project />");
        workspace.AddFile("App/Widget.cs");

        var solution = workspace.AddFile(
            "App.slnx",
            """
            <Solution>
              <Project Path="App/App.csproj" />
            </Solution>
            """);

        Assert.Equal(["Widget.cs"], NamesOf(SourceFileLocator.Locate(solution)));
    }

    [Fact]
    public void Solution_SkipsProjectsThatDoNotExist()
    {
        // A stale entry must not fail the whole run.
        using var workspace = new TemporaryWorkspace();
        workspace.AddFile("App/App.csproj", "<Project />");
        workspace.AddFile("App/Widget.cs");

        var solution = workspace.AddFile(
            "App.sln",
            """
            Project("{FAE04EC0}") = "App", "App\App.csproj", "{111}"
            Project("{FAE04EC0}") = "Gone", "Gone\Gone.csproj", "{222}"
            """);

        Assert.Equal(["Widget.cs"], NamesOf(SourceFileLocator.Locate(solution)));
    }

    [Fact]
    public void Solution_ListsEachFileOnceWhenProjectsShareADirectory()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.AddFile("App/App.csproj", "<Project />");
        workspace.AddFile("App/App.Tests.csproj", "<Project />");
        workspace.AddFile("App/Widget.cs");

        var solution = workspace.AddFile(
            "App.sln",
            """
            Project("{FAE04EC0}") = "App", "App\App.csproj", "{111}"
            Project("{FAE04EC0}") = "Tests", "App\App.Tests.csproj", "{222}"
            """);

        Assert.Single(SourceFileLocator.Locate(solution));
    }

    [Theory]
    [InlineData("notes.txt")]
    [InlineData("data.json")]
    public void UnsupportedExtension_FindsNothing(string relativePath)
    {
        using var workspace = new TemporaryWorkspace();
        var file = workspace.AddFile(relativePath, "not code");

        Assert.Empty(SourceFileLocator.Locate(file));
    }

    [Fact]
    public void MissingPath_FindsNothing()
    {
        using var workspace = new TemporaryWorkspace();

        Assert.Empty(SourceFileLocator.Locate(workspace.Combine("does-not-exist")));
    }

    [Fact]
    public void EmptyDirectory_FindsNothing()
    {
        using var workspace = new TemporaryWorkspace();

        Assert.Empty(SourceFileLocator.Locate(workspace.Root));
    }

    [Fact]
    public void ResultsAreFullyQualifiedPaths()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.AddFile("Widget.cs");

        Assert.All(
            SourceFileLocator.Locate(workspace.Root),
            path => Assert.True(Path.IsPathFullyQualified(path), path));
    }

    /// <summary>File names, sorted, so a test never depends on enumeration order.</summary>
    private static string[] NamesOf(IReadOnlyList<string> paths) =>
        paths.Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal).ToArray();
}
