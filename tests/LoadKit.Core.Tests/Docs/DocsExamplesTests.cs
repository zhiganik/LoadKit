using System.Text.Json.Nodes;
using LoadKit.Core.Scenarios;
using LoadKit.Core.Scenarios.Templates;
using LoadKit.Core.Tests.TestSupport;

namespace LoadKit.Core.Tests.Docs;

/// <summary>
/// JSON examples in user-facing docs and sample scenarios must pass validation.
/// Markers before a code block: <c>&lt;!-- fragment:auth --&gt;</c> (an <c>auth</c> object) and
/// <c>&lt;!-- no-validate --&gt;</c> (skipped). See docs/standards/DOCUMENTATION.md.
/// </summary>
[Trait("Category", "Docs")]
public sealed class DocsExamplesTests
{
    private const string FragmentAuthMarker = "<!-- fragment:auth -->";
    private const string NoValidateMarker = "<!-- no-validate -->";

    public static TheoryData<string, int> JsonExamples
    {
        get
        {
            var data = new TheoryData<string, int>();
            foreach (var relativePath in DocumentsWithExamples())
            {
                foreach (var example in ReadJsonExamples(relativePath))
                {
                    data.Add(relativePath, example.LineNumber);
                }
            }

            return data;
        }
    }

    public static TheoryData<string> SampleScenarios =>
        [.. Directory.GetFiles(RepositoryPaths.Combine("samples", "scenarios"), "*.json").Select(Path.GetFileName).OfType<string>().Order()];

    [Fact]
    public void ScenarioReference_HasExamplesOfEveryKind()
    {
        var examples = ReadJsonExamples(Path.Combine("ai", "skills", "loadtest", "SCENARIO_REFERENCE.md")).ToList();

        Assert.Contains(examples, example => example.Kind == ExampleKind.Scenario);
        Assert.Equal(6, examples.Count(example => example.Kind == ExampleKind.AuthFragment));
    }

    [Theory]
    [MemberData(nameof(JsonExamples))]
    public void JsonExample_IsValid(string relativePath, int lineNumber)
    {
        var example = ReadJsonExamples(relativePath).Single(candidate => candidate.LineNumber == lineNumber);
        if (example.Kind == ExampleKind.Skipped)
        {
            return;
        }

        var scenarioJson = example.Kind == ExampleKind.AuthFragment ? WrapAuthFragment(example.Json) : example.Json;
        var result = LoadWithAnyVariables(scenarioJson);

        Assert.True(result.IsValid, $"{relativePath}:{lineNumber}: {DescribeErrors(result)}");
    }

    [Theory]
    [MemberData(nameof(SampleScenarios))]
    public async Task SampleScenario_IsValidWithExampleEnv(string fileName)
    {
        var samplesDirectory = RepositoryPaths.Combine("samples", "scenarios");
        var loader = new ScenarioLoader(TemplateRegistry.CreateDefault(), _ => null);

        var result = await loader.LoadAsync(
            Path.Combine(samplesDirectory, fileName),
            Path.Combine(samplesDirectory, ".env.example"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid, $"{fileName}: {DescribeErrors(result)}");
    }

    private static IEnumerable<string> DocumentsWithExamples()
    {
        yield return "README.md";
        yield return Path.Combine("ai", "skills", "loadtest", "SCENARIO_REFERENCE.md");
        foreach (var userDocument in Directory.GetFiles(RepositoryPaths.Combine("docs", "user"), "*.md").Order())
        {
            yield return Path.GetRelativePath(RepositoryPaths.Root, userDocument);
        }
    }

    private static IEnumerable<JsonExample> ReadJsonExamples(string relativePath)
    {
        var lines = File.ReadAllLines(RepositoryPaths.Combine(relativePath));
        for (var index = 0; index < lines.Length; index++)
        {
            if (lines[index].Trim() != "```json")
            {
                continue;
            }

            var startLine = index + 1;
            var marker = PreviousNonEmptyLine(lines, index);
            var body = new List<string>();
            for (index++; index < lines.Length && lines[index].Trim() != "```"; index++)
            {
                body.Add(lines[index]);
            }

            var kind = marker switch
            {
                FragmentAuthMarker => ExampleKind.AuthFragment,
                NoValidateMarker => ExampleKind.Skipped,
                _ => ExampleKind.Scenario,
            };
            yield return new JsonExample(startLine, kind, string.Join('\n', body));
        }
    }

    private static string PreviousNonEmptyLine(string[] lines, int index)
    {
        for (var previous = index - 1; previous >= 0; previous--)
        {
            if (lines[previous].Trim().Length > 0)
            {
                return lines[previous].Trim();
            }
        }

        return string.Empty;
    }

    private static string WrapAuthFragment(string authJson)
    {
        var scenario = TestScenarios.Minimal();
        scenario["auth"] = JsonNode.Parse(authJson);
        return scenario.ToJsonString();
    }

    private static ScenarioLoadResult LoadWithAnyVariables(string scenarioJson)
    {
        var loader = new ScenarioLoader(TemplateRegistry.CreateDefault(), _ => "example-value");
        return loader.LoadFromJson(scenarioJson, new Dictionary<string, string>());
    }

    private static string DescribeErrors(ScenarioLoadResult result)
    {
        return string.Join("; ", result.Issues
            .Where(issue => issue.Severity == ValidationSeverity.Error)
            .Select(issue => $"{issue.Path} ({issue.Code}): {issue.Message}"));
    }

    private enum ExampleKind
    {
        Scenario,
        AuthFragment,
        Skipped,
    }

    private sealed record JsonExample(int LineNumber, ExampleKind Kind, string Json);
}
