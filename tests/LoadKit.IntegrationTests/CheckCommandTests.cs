namespace LoadKit.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class CheckCommandTests(TargetApiFixture targetApi) : IClassFixture<TargetApiFixture>, IDisposable
{
    private const int ExitSuccess = 0;
    private const int ExitPreflightFailed = 3;
    private const int ExitConfirmationRequired = 4;

    private readonly string _directory = Directory.CreateTempSubdirectory("loadkit-check-").FullName;

    [Fact]
    public async Task Login_ShowsEachRequest_WithStatusAndBody()
    {
        File.WriteAllText(Path.Combine(_directory, ".env"), "LOADKIT_IT_USER=loadtest@example.com\nLOADKIT_IT_PASSWORD=dev-password\n");
        var scenarioPath = WriteScenario($$"""
            {
              "version": 1, "name": "check", "baseUrl": "{{targetApi.BaseUrl}}",
              "auth": {
                "type": "login",
                "request": { "method": "POST", "path": "/auth/login", "body": { "email": "${env:LOADKIT_IT_USER}", "password": "${env:LOADKIT_IT_PASSWORD}" } },
                "tokenPath": "$.accessToken", "expiresInPath": "$.expiresIn"
              },
              "load": { "concurrency": 10, "totalRequests": 1000 },
              "requests": [
                { "name": "secure", "method": "GET", "path": "/secure", "weight": 9, "expect": { "status": [200] } },
                { "name": "fail", "method": "GET", "path": "/api/fail", "query": { "probability": "1" }, "expect": { "status": [200] } }
              ]
            }
            """);

        var result = await CliRunner.RunAsync(["check", scenarioPath], TestContext.Current.CancellationToken);

        Assert.True(result.ExitCode == ExitSuccess, result.ToString());
        Assert.Contains($"ok baseUrl: {targetApi.BaseUrl} is reachable", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("ok auth: login: token acquired, expires in 10 s", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains($"ok secure GET {targetApi.BaseUrl}/secure -> 200 in", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("""{"result":"secure"}""", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("UNEXPECTED fail GET", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("-> 500 in", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("1 of 2 request(s) did not return an expected status.", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("dev-password", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnreachableBaseUrl_ExitsThree()
    {
        var scenarioPath = WriteScenario("""
            {
              "version": 1, "name": "down", "baseUrl": "http://127.0.0.1:1",
              "load": { "concurrency": 1, "totalRequests": 1 },
              "requests": [ { "name": "health", "method": "GET", "path": "/health", "expect": { "status": [200] } } ]
            }
            """);

        var result = await CliRunner.RunAsync(["check", scenarioPath], TestContext.Current.CancellationToken);

        Assert.True(result.ExitCode == ExitPreflightFailed, result.ToString());
        Assert.Contains("hint: check that the API is running", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemoteUrl_WithoutYes_ExitsFour()
    {
        var scenarioPath = WriteScenario("""
            {
              "version": 1, "name": "remote", "baseUrl": "https://loadkit-remote.invalid",
              "load": { "concurrency": 1, "totalRequests": 1 },
              "requests": [ { "name": "root", "method": "GET", "path": "/", "expect": { "status": [200] } } ]
            }
            """);

        var result = await CliRunner.RunAsync(["check", scenarioPath], TestContext.Current.CancellationToken);

        Assert.True(result.ExitCode == ExitConfirmationRequired, result.ToString());
        Assert.Contains("retry with --yes", result.StandardOutput, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    private string WriteScenario(string json)
    {
        var path = Path.Combine(_directory, "scenario.json");
        File.WriteAllText(path, json);
        return path;
    }
}
