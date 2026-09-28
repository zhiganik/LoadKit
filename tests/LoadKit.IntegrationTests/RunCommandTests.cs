namespace LoadKit.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class RunCommandTests(TargetApiFixture targetApi) : IClassFixture<TargetApiFixture>, IDisposable
{
    private const int ExitSuccess = 0;
    private const int ExitThresholdsFailed = 1;
    private const int ExitInvalidScenario = 2;
    private const int ExitPreflightFailed = 3;
    private const int ExitConfirmationRequired = 4;
    private const string DevToken = "dev-token";
    private const string DevApiKey = "dev-api-key";

    private readonly string _directory = Directory.CreateTempSubdirectory("loadkit-run-").FullName;

    [Fact]
    public async Task Mix_ShowsPercentiles_AndExpected500sAreNotErrors()
    {
        var scenarioPath = WriteScenario("mix.json", $$"""
            {
              "version": 1, "name": "mix", "baseUrl": "{{BaseUrl}}",
              "load": { "concurrency": 4, "totalRequests": 120, "warmup": 10 },
              "requests": [
                { "name": "fast", "method": "GET", "path": "/api/fast", "weight": 3, "expect": { "status": [200] } },
                { "name": "fail", "method": "GET", "path": "/api/fail", "weight": 1, "expect": { "status": [200, 500] } }
              ],
              "thresholds": { "p95Ms": 5000, "errorRatePercent": 0 }
            }
            """);

        var result = await RunAsync(scenarioPath);

        Assert.True(result.ExitCode == ExitSuccess, result.ToString());
        Assert.Contains("110 measured requests", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("| p50 ms | p95 ms | p99 ms |", result.StandardOutput, StringComparison.Ordinal);
        Assert.Matches(@"\| fail\s+\|", result.StandardOutput);
        Assert.Contains("loadrun=", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("UnexpectedStatus", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Thresholds passed.", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unexpected500s_AreErrors_ThresholdFails_ExitsOne()
    {
        var scenarioPath = WriteScenario("fail.json", $$"""
            {
              "version": 1, "name": "fail", "baseUrl": "{{BaseUrl}}",
              "load": { "concurrency": 2, "totalRequests": 20 },
              "requests": [
                { "name": "fail", "method": "GET", "path": "/api/fail", "query": { "probability": "1" }, "expect": { "status": [200] } }
              ],
              "thresholds": { "errorRatePercent": 0 }
            }
            """);

        var result = await RunAsync(scenarioPath, "--no-tag");

        Assert.True(result.ExitCode == ExitThresholdsFailed, result.ToString());
        Assert.Contains("| UnexpectedStatus |    20 |", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("500 fail (UnexpectedStatus): ", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Thresholds failed: errorRatePercent", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("loadrun=", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Bearer_AndAuthFalse_AgainstSecure_ExitsZero()
    {
        File.WriteAllText(Path.Combine(_directory, ".env"), $"LOADKIT_IT_TOKEN={DevToken}\n");
        var scenarioPath = WriteScenario("bearer.json", $$"""
            {
              "version": 1, "name": "bearer", "baseUrl": "{{BaseUrl}}",
              "auth": { "type": "bearer", "token": "${env:LOADKIT_IT_TOKEN}" },
              "load": { "concurrency": 2, "totalRequests": 40 },
              "requests": [
                { "name": "secure", "method": "GET", "path": "/secure", "weight": 3, "expect": { "status": [200] } },
                { "name": "no-auth", "method": "GET", "path": "/secure", "weight": 1, "auth": false, "expect": { "status": [401] } }
              ],
              "thresholds": { "errorRatePercent": 0 }
            }
            """);

        var result = await RunAsync(scenarioPath);

        Assert.True(result.ExitCode == ExitSuccess, result.ToString());
        Assert.DoesNotContain(DevToken, result.StandardOutput, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{ "type": "apiKey", "header": "x-api-key", "value": "${env:LOADKIT_IT_KEY}" }""", ExitSuccess)]
    [InlineData("""{ "type": "apiKey", "header": "x-api-key", "value": "${env:LOADKIT_IT_WRONG_KEY}" }""", ExitThresholdsFailed)]
    public async Task ApiKeyHeader_AgainstSecure(string auth, int expectedExitCode)
    {
        File.WriteAllText(Path.Combine(_directory, ".env"), $"LOADKIT_IT_KEY={DevApiKey}\nLOADKIT_IT_WRONG_KEY=wrong-api-key\n");
        var scenarioPath = WriteScenario("apikey.json", $$"""
            {
              "version": 1, "name": "apikey", "baseUrl": "{{BaseUrl}}",
              "auth": {{auth}},
              "load": { "concurrency": 2, "totalRequests": 20 },
              "requests": [ { "name": "secure", "method": "GET", "path": "/secure", "expect": { "status": [200] } } ],
              "thresholds": { "errorRatePercent": 0 }
            }
            """);

        var result = await RunAsync(scenarioPath);

        Assert.True(result.ExitCode == expectedExitCode, result.ToString());
        Assert.DoesNotContain("wrong-api-key", result.StandardOutput, StringComparison.Ordinal);
        if (expectedExitCode == ExitThresholdsFailed)
        {
            Assert.Contains("unexpected 401 responses", result.StandardOutput, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task RemoteUrl_WithoutTerminalAndYes_ExitsFour_WithoutSending()
    {
        var scenarioPath = WriteScenario("remote.json", """
            {
              "version": 1, "name": "remote", "baseUrl": "https://loadkit-remote.invalid",
              "load": { "concurrency": 2, "totalRequests": 10 },
              "requests": [ { "name": "root", "method": "GET", "path": "/", "expect": { "status": [200] } } ]
            }
            """);

        var result = await RunAsync(scenarioPath);

        Assert.True(result.ExitCode == ExitConfirmationRequired, result.ToString());
        Assert.Contains("retry with --yes", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Running", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConflictingOverrides_ExitTwo()
    {
        var scenarioPath = WriteScenario("smoke.json", $$"""
            {
              "version": 1, "name": "smoke", "baseUrl": "{{BaseUrl}}",
              "load": { "concurrency": 2, "totalRequests": 10 },
              "requests": [ { "name": "health", "method": "GET", "path": "/health", "expect": { "status": [200] } } ]
            }
            """);

        var result = await RunAsync(scenarioPath, "--total", "5", "--duration", "5");

        Assert.True(result.ExitCode == ExitInvalidScenario, result.ToString());
        Assert.Contains("--total (load-mode)", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DurationOverride_RunsForDuration()
    {
        var scenarioPath = WriteScenario("smoke.json", $$"""
            {
              "version": 1, "name": "smoke", "baseUrl": "{{BaseUrl}}",
              "load": { "concurrency": 2, "totalRequests": 10 },
              "requests": [ { "name": "health", "method": "GET", "path": "/health", "expect": { "status": [200] } } ]
            }
            """);

        var result = await RunAsync(scenarioPath, "--duration", "1", "--concurrency", "3");

        Assert.True(result.ExitCode == ExitSuccess, result.ToString());
        Assert.Contains("concurrency 3, 1 s", result.StandardOutput, StringComparison.Ordinal);
    }

    // Until phase 3 implements token providers, `run` reports them as a preflight failure.
    [Fact]
    public async Task UnsupportedAuthType_ExitsThree()
    {
        var scenarioPath = WriteScenario("azure.json", $$"""
            {
              "version": 1, "name": "azure", "baseUrl": "{{BaseUrl}}",
              "auth": { "type": "azureIdentity", "scope": "api://target/.default" },
              "load": { "concurrency": 1, "totalRequests": 1 },
              "requests": [ { "name": "secure", "method": "GET", "path": "/secure", "expect": { "status": [200] } } ]
            }
            """);

        var result = await RunAsync(scenarioPath);

        Assert.True(result.ExitCode == ExitPreflightFailed, result.ToString());
        Assert.Contains("preflight failed", result.StandardOutput, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    private string BaseUrl => targetApi.BaseAddress.GetLeftPart(UriPartial.Authority);

    private string WriteScenario(string fileName, string json)
    {
        var fullPath = Path.Combine(_directory, fileName);
        File.WriteAllText(fullPath, json);
        return fullPath;
    }

    private static Task<CliResult> RunAsync(string scenarioPath, params string[] flags)
    {
        return CliRunner.RunAsync(["run", scenarioPath, .. flags], TestContext.Current.CancellationToken);
    }
}
