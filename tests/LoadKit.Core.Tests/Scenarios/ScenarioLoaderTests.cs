using System.Text.Json.Nodes;
using LoadKit.Core.Scenarios;
using LoadKit.Core.Scenarios.Model;
using LoadKit.Core.Scenarios.Templates;
using LoadKit.Core.Tests.TestSupport;

namespace LoadKit.Core.Tests.Scenarios;

public sealed class ScenarioLoaderTests
{
    [Fact]
    public void Defaults_AreApplied()
    {
        var result = TestScenarios.Load(TestScenarios.Minimal());

        var scenario = result.Scenario!.Scenario;
        Assert.True(scenario.TagRuns);
        Assert.Null(scenario.Auth);
        Assert.Null(scenario.Thresholds);
        Assert.Equal(new LoadOptions(2, 10, null, 0, 30_000), scenario.Load);
        var request = Assert.Single(scenario.Requests);
        Assert.Equal(1, request.Weight);
        Assert.True(request.UseAuth);
        Assert.False(request.AllowEmptyBody);
        Assert.Equal("application/json", request.ContentType);
        Assert.Equal([200], request.Expect.Status);
    }

    [Fact]
    public void AllFields_AreBound()
    {
        var result = TestScenarios.Load("""
            {
              "version": 1,
              "name": "full",
              "description": "all fields",
              "baseUrl": "http://localhost:5080",
              "headers": { "Accept": "application/json" },
              "auth": { "type": "login", "request": { "method": "post", "path": "/auth/login", "body": { "password": "${env:PASSWORD}" } },
                        "tokenPath": "$.accessToken", "expiresInPath": "$.expiresIn" },
              "tagRuns": false,
              "load": { "concurrency": 3, "durationSec": 30, "warmup": 5, "timeoutMs": 1000 },
              "requests": [
                { "name": "create", "method": "post", "path": "/orders", "weight": 3,
                  "headers": { "X-Id": "{{guid}}" }, "query": { "page": 2, "draft": true },
                  "body": { "qty": 1 }, "auth": false, "expect": { "status": [201, 202], "maxMs": 500 } }
              ],
              "thresholds": { "p50Ms": 100, "p95Ms": 200, "p99Ms": 300, "errorRatePercent": 0.5 }
            }
            """,
            new Dictionary<string, string> { ["PASSWORD"] = "pw" });

        Assert.True(result.IsValid, string.Join("; ", result.Issues.Select(issue => issue.Message)));
        var scenario = result.Scenario!.Scenario;
        Assert.Equal("all fields", scenario.Description);
        Assert.False(scenario.TagRuns);
        Assert.Equal(new LoadOptions(3, null, 30, 5, 1000), scenario.Load);
        Assert.Equal(new Thresholds(100, 200, 300, 0.5), scenario.Thresholds);

        var login = Assert.IsType<LoginAuth>(scenario.Auth);
        Assert.Equal("POST", login.Request.Method);
        Assert.Equal("$.expiresIn", login.ExpiresInPath);
        Assert.Equal("Authorization", login.Header);
        Assert.Equal("Bearer {token}", login.Format);
        Assert.Equal("pw", login.Request.Body!.Value.GetProperty("password").GetString());

        var request = Assert.Single(scenario.Requests);
        Assert.Equal("POST", request.Method);
        Assert.Equal(3, request.Weight);
        Assert.False(request.UseAuth);
        Assert.Equal("2", request.Query["page"]);
        Assert.Equal("true", request.Query["draft"]);
        Assert.Equal([201, 202], request.Expect.Status);
        Assert.Equal(500, request.Expect.MaxMs);
    }

    [Fact]
    public void Compile_MergesHeadersAndCompilesBody()
    {
        var scenario = TestScenarios.Minimal();
        scenario["headers"] = new JsonObject { ["Accept"] = "text/plain", ["X-Root"] = "root" };
        var request = TestScenarios.FirstRequest(scenario);
        request["method"] = "POST";
        request["path"] = "/orders/{{seq}}";
        request["headers"] = new JsonObject { ["accept"] = "application/json" };
        request["body"] = new JsonObject { ["id"] = "{{seq}}" };

        var compiledScenario = TestScenarios.Load(scenario).Scenario!;
        var compiled = Assert.Single(compiledScenario.Requests);
        var context = new TemplateContext(TimeProvider.System, Random.Shared);

        Assert.Equal(new Uri("http://localhost:5080"), compiledScenario.BaseUri);
        Assert.Equal("/orders/1", compiled.Path.Render(context));
        Assert.Equal("""{"id":2}""", compiled.Body!.Render(context));
        Assert.Equal("application/json", compiled.BodyContentType);
        Assert.Equal(2, compiled.Headers.Count);
        Assert.Equal("application/json", Assert.Single(compiled.Headers, header => header.Name.Equals("Accept", StringComparison.OrdinalIgnoreCase)).Value.Render(context));
    }

    [Fact]
    public void Compile_BodyRawUsesContentType()
    {
        var scenario = TestScenarios.Minimal();
        var request = TestScenarios.FirstRequest(scenario);
        request["method"] = "PUT";
        request["bodyRaw"] = "id={{seq}}";
        request["contentType"] = "application/x-www-form-urlencoded";

        var compiled = Assert.Single(TestScenarios.Load(scenario).Scenario!.Requests);

        Assert.Equal("id=1", compiled.Body!.Render(new TemplateContext(TimeProvider.System, Random.Shared)));
        Assert.Equal("application/x-www-form-urlencoded", compiled.BodyContentType);
    }

    [Fact]
    public void ToString_DoesNotExposeSecrets()
    {
        var scenario = TestScenarios.Minimal();
        scenario["auth"] = JsonNode.Parse("""{ "type": "bearer", "token": "${env:API_TOKEN}" }""");

        var loaded = TestScenarios.Load(scenario, new Dictionary<string, string> { ["API_TOKEN"] = "very-secret-token" }).Scenario!;

        Assert.DoesNotContain("very-secret-token", loaded.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("very-secret-token", loaded.Scenario.Auth!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_MissingFile_IsFileNotFound()
    {
        var result = await new ScenarioLoader().LoadAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"), null, TestContext.Current.CancellationToken);

        Assert.Equal(ValidationCodes.FileNotFound, Assert.Single(result.Issues).Code);
    }

    [Fact]
    public async Task LoadAsync_MissingExplicitEnvFile_IsEnvFileNotFound()
    {
        using var directory = new TemporaryDirectory();
        var scenarioPath = directory.WriteFile("scenario.json", TestScenarios.MinimalJson);

        var result = await new ScenarioLoader().LoadAsync(scenarioPath, Path.Combine(directory.Path, "missing.env"), TestContext.Current.CancellationToken);

        Assert.Equal(ValidationCodes.EnvFileNotFound, Assert.Single(result.Issues).Code);
    }

    [Fact]
    public async Task LoadAsync_UsesEnvFileFromParentFolder_ProcessVariableWins()
    {
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".env", "FROM_FILE=file-value\nBOTH=file-value\n");
        var scenario = TestScenarios.Minimal();
        scenario["headers"] = new JsonObject { ["X-File"] = "${env:FROM_FILE}", ["X-Both"] = "${env:BOTH}" };
        var scenarioPath = directory.WriteFile(Path.Combine("scenarios", "api.json"), scenario.ToJsonString());
        var loader = new ScenarioLoader(TemplateRegistry.CreateDefault(), name => name == "BOTH" ? "process-value" : null);

        var result = await loader.LoadAsync(scenarioPath, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
        Assert.Equal("file-value", result.Scenario!.Scenario.Headers["X-File"]);
        Assert.Equal("process-value", result.Scenario.Scenario.Headers["X-Both"]);
    }
}
