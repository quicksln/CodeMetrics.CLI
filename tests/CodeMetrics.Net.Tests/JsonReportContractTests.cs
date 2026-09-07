using System.Text.Json;
using CodeMetrics.Analysis;
using CodeMetrics.Cli;
using CodeMetrics.Net.Tests.TestSupport;
using Xunit;

namespace CodeMetrics.Net.Tests;

/// <summary>
/// The json report is the machine-readable contract other tools parse, so its
/// shape is pinned exactly: which properties exist, what they are called, in
/// what order, and what type each one carries.
/// </summary>
public class JsonReportContractTests
{
    /// <summary>
    /// Declaration order of <c>MemberMetrics</c>. Reordering these would break
    /// diffs of committed reports even though the data is unchanged.
    /// </summary>
    private static readonly string[] ExpectedProperties =
    [
        "filePath",
        "lineNumber",
        "typeName",
        "memberName",
        "cyclomaticComplexity",
        "cognitiveComplexity",
        "linesOfCode",
        "maxNestingDepth",
        "parameterCount",
        "maintainabilityIndex",
        "fullName"
    ];

    [Fact]
    public void Json_IsAnArrayOfMembersOnly()
    {
        // The source summary is deliberately absent: json carries members, the
        // html dashboard carries the inventory.
        using var document = JsonDocument.Parse(Render(ReportFixture.Member(), ReportFixture.Member("Other")));

        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        Assert.Equal(2, document.RootElement.GetArrayLength());
    }

    [Fact]
    public void Member_CarriesExactlyTheDocumentedPropertiesInOrder()
    {
        using var document = JsonDocument.Parse(Render(ReportFixture.Member()));

        var actual = document.RootElement[0]
            .EnumerateObject()
            .Select(property => property.Name)
            .ToArray();

        Assert.Equal(ExpectedProperties, actual);
    }

    [Fact]
    public void Member_CarriesTheMeasuredValues()
    {
        var member = ReportFixture.Member(
            memberName: "Run",
            typeName: "Example",
            filePath: "sample.cs",
            lineNumber: 10,
            cognitive: 4,
            cyclomatic: 3,
            linesOfCode: 6,
            maxNestingDepth: 2,
            parameterCount: 1,
            maintainabilityIndex: 70.5);

        using var document = JsonDocument.Parse(Render(member));
        var element = document.RootElement[0];

        Assert.Equal("sample.cs", element.GetProperty("filePath").GetString());
        Assert.Equal(10, element.GetProperty("lineNumber").GetInt32());
        Assert.Equal("Example", element.GetProperty("typeName").GetString());
        Assert.Equal("Run", element.GetProperty("memberName").GetString());
        Assert.Equal(3, element.GetProperty("cyclomaticComplexity").GetInt32());
        Assert.Equal(4, element.GetProperty("cognitiveComplexity").GetInt32());
        Assert.Equal(6, element.GetProperty("linesOfCode").GetInt32());
        Assert.Equal(2, element.GetProperty("maxNestingDepth").GetInt32());
        Assert.Equal(1, element.GetProperty("parameterCount").GetInt32());
        Assert.Equal(70.5, element.GetProperty("maintainabilityIndex").GetDouble());
    }

    [Fact]
    public void FullName_IsSerialisedEvenThoughItIsComputed()
    {
        // Consumers key on fullName; losing the computed property to a refactor
        // would silently break them.
        using var document = JsonDocument.Parse(Render(ReportFixture.Member("Run", "Example")));

        Assert.Equal("Example.Run", document.RootElement[0].GetProperty("fullName").GetString());
    }

    [Fact]
    public void Numbers_AreJsonNumbersNotStrings()
    {
        using var document = JsonDocument.Parse(Render(ReportFixture.Member()));
        var element = document.RootElement[0];

        foreach (var name in new[]
                 {
                     "lineNumber", "cyclomaticComplexity", "cognitiveComplexity",
                     "linesOfCode", "maxNestingDepth", "parameterCount", "maintainabilityIndex"
                 })
        {
            Assert.Equal(JsonValueKind.Number, element.GetProperty(name).ValueKind);
        }
    }

    [Fact]
    public void EmptyReport_IsAnEmptyArrayNotNull()
    {
        var json = Render();

        Assert.Equal("[]", json.Trim());
    }

    [Fact]
    public void Json_IsIndented()
    {
        var json = Render(ReportFixture.Member());

        Assert.Contains("\n  {", json.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    private static string Render(params MemberMetrics[] members) =>
        ReportFixture.Render(OutputFormat.Json, members);
}
