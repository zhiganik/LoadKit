using LoadKit.Core.Scenarios;
using LoadKit.Core.Scenarios.Scaffolding;
using LoadKit.Core.Tests.TestSupport;

namespace LoadKit.Core.Tests.Scenarios;

public sealed class ScaffoldingTests
{
    private const string SchemaJson = """{ "title": "schema" }""";

    [Theory]
    [InlineData("none", null)]
    [InlineData("bearer", null)]
    [InlineData("apiKey", null)]
    [InlineData("azureIdentity", "api://my-api/.default")]
    [InlineData("oauth2ClientCredentials", "api://my-api/.default")]
    [InlineData("login", null)]
    public void GeneratedScenario_IsValid_OnceVariablesAreFilled(string authType, string? scope)
    {
        var options = new InitOptions("loadtests/scenarios/my-api.json", "http://localhost:5000", authType, null, scope, null);
        Assert.Empty(ScenarioScaffolder.Validate(options));

        var json = ScenarioScaffolder.CreateScenarioJson(options, "../scenario.schema.json");
        var variables = ScenarioScaffolder.EnvVariables(authType).ToDictionary(name => name, name => name == "TOKEN_URL" ? "https://login.example.com/token" : "filled-value");
        var result = TestScenarios.Load(json, variables);

        Assert.True(result.IsValid, string.Join("; ", result.Issues));
        Assert.Equal(authType == "none" ? null : authType, result.Scenario!.Scenario.Auth?.Type);
        Assert.DoesNotContain("filled-value", json, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratedScenario_WithoutVariables_ReportsThemAsMissing()
    {
        var options = new InitOptions("s.json", "http://localhost:5000", "login", null, null, null);

        var result = TestScenarios.Load(ScenarioScaffolder.CreateScenarioJson(options, "scenario.schema.json"));

        Assert.Equal(["LOGIN_USER", "LOGIN_PASSWORD"], result.Issues.Where(issue => issue.Code == ValidationCodes.EnvMissing).Select(issue => issue.Message.Split(' ')[2]));
    }

    [Theory]
    [InlineData("ftp://host", "none", null, "--base-url")]
    [InlineData("http://localhost", "token", null, "--auth")]
    [InlineData("http://localhost", "azureIdentity", null, "--scope")]
    [InlineData("http://localhost", "oauth2ClientCredentials", null, "--scope")]
    public void Validate_RejectsBadAnswers(string baseUrl, string authType, string? scope, string expectedPath)
    {
        var issue = Assert.Single(ScenarioScaffolder.Validate(new InitOptions("s.json", baseUrl, authType, null, scope, null)));

        Assert.Equal(expectedPath, issue.Path);
    }

    [Fact]
    public async Task Initialize_CreatesLayout_WithSchemaReferenceAndEmptyVariables()
    {
        using var directory = new TemporaryDirectory();
        var initializer = new WorkspaceInitializer(directory.Path);
        var options = new InitOptions("loadtests/scenarios/my-api.json", "http://localhost:5000", "bearer", null, null, null);

        var result = await initializer.InitializeAsync(options, SchemaJson, TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                ("loadtests/scenario.schema.json", "created"),
                ("loadtests/scenarios/my-api.json", "created"),
                ("loadtests/.env", "created"),
                (".gitignore", "created"),
            ],
            result.Changes.Select(change => (change.Path, change.Change)));
        Assert.Equal(SchemaJson, File.ReadAllText(Path.Combine(directory.Path, "loadtests", "scenario.schema.json")));
        Assert.Contains("\"$schema\": \"../scenario.schema.json\"", File.ReadAllText(Path.Combine(directory.Path, "loadtests", "scenarios", "my-api.json")), StringComparison.Ordinal);
        Assert.EndsWith("\nAPI_TOKEN=\n", File.ReadAllText(Path.Combine(directory.Path, "loadtests", ".env")), StringComparison.Ordinal);
        Assert.Equal("# LoadKit: secrets and run reports\nloadtests/.env\nloadtests/reports/\n", File.ReadAllText(Path.Combine(directory.Path, ".gitignore")));
        Assert.Equal(["API_TOKEN"], result.EmptyVariables);
        Assert.Equal(("loadtests/.env", "loadtests/reports/"), (result.EnvFilePath, result.ReportsPath));
        Assert.Empty(initializer.CheckTarget(new InitOptions("loadtests/scenarios/other.json", "http://localhost", "none", null, null, null)));
        Assert.Single(initializer.CheckTarget(options));
    }

    [Fact]
    public async Task Initialize_Again_KeepsExistingValuesAndGitIgnore()
    {
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".gitignore", "bin/\nloadtests/.env");
        directory.WriteFile("loadtests/.env", "API_TOKEN=kept-secret\nOTHER=1");
        var initializer = new WorkspaceInitializer(directory.Path);

        var result = await initializer.InitializeAsync(
            new InitOptions("loadtests/scenarios/second.json", "http://localhost:5000", "oauth2ClientCredentials", null, "api://x/.default", null),
            SchemaJson,
            TestContext.Current.CancellationToken);

        Assert.Equal("API_TOKEN=kept-secret\nOTHER=1\nTOKEN_URL=\nCLIENT_ID=\nCLIENT_SECRET=\n", File.ReadAllText(Path.Combine(directory.Path, "loadtests", ".env")));
        Assert.Equal("bin/\nloadtests/.env\n\n# LoadKit: secrets and run reports\nloadtests/reports/\n", File.ReadAllText(Path.Combine(directory.Path, ".gitignore")));
        Assert.Equal(["TOKEN_URL", "CLIENT_ID", "CLIENT_SECRET"], result.EmptyVariables);

        var third = await initializer.InitializeAsync(
            new InitOptions("loadtests/scenarios/third.json", "http://localhost:5000", "none", null, null, null),
            SchemaJson,
            TestContext.Current.CancellationToken);

        Assert.Equal(["unchanged", "created", "unchanged", "unchanged"], third.Changes.Select(change => change.Change));
    }

    [Fact]
    public async Task Initialize_UsesGitRoot_ForGitIgnore()
    {
        using var directory = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(directory.Path, "repo", ".git"));
        var initializer = new WorkspaceInitializer(Path.Combine(directory.Path, "repo", "src", "api"));

        await initializer.InitializeAsync(
            new InitOptions("loadtests/scenarios/api.json", "http://localhost:5000", "none", null, null, null),
            SchemaJson,
            TestContext.Current.CancellationToken);

        Assert.Contains("src/api/loadtests/.env", File.ReadAllText(Path.Combine(directory.Path, "repo", ".gitignore")), StringComparison.Ordinal);
    }

    [Fact]
    public void WorkspaceRoot_IsScenarioFolder_WhenNotInScenariosFolder()
    {
        using var directory = new TemporaryDirectory();
        var initializer = new WorkspaceInitializer(directory.Path);

        Assert.Equal(
            Path.Combine(directory.Path, "perf"),
            initializer.WorkspaceRoot(new InitOptions("perf/api.json", "http://localhost", "none", null, null, null)));
    }
}
