namespace LoadKit.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class ValidateCommandTests : IDisposable
{
    private const int ExitSuccess = 0;
    private const int ExitInvalidScenario = 2;

    private readonly string _directory = Directory.CreateTempSubdirectory("loadkit-cli-").FullName;

    [Fact]
    public async Task ValidScenario_ExitsZero()
    {
        var scenarioPath = WriteScenario("smoke.json", """
            {
              "version": 1,
              "name": "smoke",
              "baseUrl": "http://localhost:5080",
              "load": { "concurrency": 2, "totalRequests": 10 },
              "requests": [ { "name": "health", "method": "GET", "path": "/health", "expect": { "status": [200] } } ]
            }
            """);

        var result = await RunValidateAsync(scenarioPath);

        Assert.True(result.ExitCode == ExitSuccess, result.ToString());
        Assert.Contains("Valid:", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThreeDifferentErrors_AreAllShown_ExitsTwo()
    {
        var scenarioPath = WriteScenario("broken.json", """
            {
              "version": 1,
              "name": "broken",
              "baseUrl": "http://localhost:5080",
              "load": { "concurrency": 2, "totalRequests": 10, "retries": 3 },
              "requests": [ { "name": "create", "method": "POST", "path": "/orders/{{uuid}}", "expect": { "status": [201] } } ]
            }
            """);

        var result = await RunValidateAsync(scenarioPath);

        Assert.True(result.ExitCode == ExitInvalidScenario, result.ToString());
        Assert.Contains("load.retries (unknown-field)", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("requests[0].body (body-required)", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("requests[0].path (unknown-template)", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("use {{guid}}", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingVariable_UsesEnvFileNextToScenario()
    {
        var scenarioPath = WriteScenario(Path.Combine("scenarios", "secure.json"), """
            {
              "version": 1,
              "name": "secure",
              "baseUrl": "http://localhost:5080",
              "auth": { "type": "bearer", "token": "${env:LOADKIT_TEST_TOKEN_7F3A}" },
              "load": { "concurrency": 1, "totalRequests": 1 },
              "requests": [ { "name": "secure", "method": "GET", "path": "/secure", "expect": { "status": [200] } } ]
            }
            """);

        var withoutEnv = await RunValidateAsync(scenarioPath);
        File.WriteAllText(Path.Combine(_directory, ".env"), "LOADKIT_TEST_TOKEN_7F3A=value-from-parent-env\n");
        var withParentEnv = await RunValidateAsync(scenarioPath);

        Assert.True(withoutEnv.ExitCode == ExitInvalidScenario, withoutEnv.ToString());
        Assert.Contains("env-missing", withoutEnv.StandardOutput, StringComparison.Ordinal);
        Assert.True(withParentEnv.ExitCode == ExitSuccess, withParentEnv.ToString());
        Assert.DoesNotContain("value-from-parent-env", withParentEnv.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingFile_ExitsTwo()
    {
        var result = await RunValidateAsync(Path.Combine(_directory, "missing.json"));

        Assert.True(result.ExitCode == ExitInvalidScenario, result.ToString());
        Assert.Contains("file-not-found", result.StandardOutput, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    private string WriteScenario(string relativePath, string json)
    {
        var fullPath = Path.Combine(_directory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, json);
        return fullPath;
    }

    private static Task<CliResult> RunValidateAsync(string scenarioPath)
    {
        return CliRunner.RunAsync(["validate", scenarioPath], TestContext.Current.CancellationToken);
    }
}
