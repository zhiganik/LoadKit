using System.Text.Json.Nodes;
using LoadKit.Core.Scenarios;
using LoadKit.Core.Scenarios.Templates;

namespace LoadKit.Core.Tests.TestSupport;

/// <summary>A minimal valid scenario that tests modify, and helpers to load it.</summary>
internal static class TestScenarios
{
    public const string MinimalJson = """
        {
          "version": 1,
          "name": "test",
          "baseUrl": "http://localhost:5080",
          "load": { "concurrency": 2, "totalRequests": 10 },
          "requests": [
            { "name": "health", "method": "GET", "path": "/health", "expect": { "status": [200] } }
          ]
        }
        """;

    public static JsonObject Minimal()
    {
        return JsonNode.Parse(MinimalJson)!.AsObject();
    }

    public static JsonObject FirstRequest(JsonObject scenario)
    {
        return scenario["requests"]![0]!.AsObject();
    }

    public static ScenarioLoadResult Load(JsonObject scenario, IReadOnlyDictionary<string, string>? envFileVariables = null)
    {
        return Load(scenario.ToJsonString(), envFileVariables);
    }

    public static ScenarioLoadResult Load(string scenarioJson, IReadOnlyDictionary<string, string>? envFileVariables = null)
    {
        var loader = new ScenarioLoader(TemplateRegistry.CreateDefault(), _ => null);
        return loader.LoadFromJson(scenarioJson, envFileVariables ?? new Dictionary<string, string>());
    }

    public static ValidationIssue SingleIssue(ScenarioLoadResult result, string code)
    {
        return Assert.Single(result.Issues, issue => issue.Code == code);
    }
}
