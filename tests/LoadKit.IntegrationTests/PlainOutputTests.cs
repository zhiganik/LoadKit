namespace LoadKit.IntegrationTests;

/// <summary>
/// Redirected output must be plain text in CI too. Spectre's CI detection (GITHUB_ACTIONS, TF_BUILD, ...) used to turn
/// ANSI back on, so "[bold]path[/]" became escape codes and every output assertion failed on GitHub Actions only.
/// </summary>
[Trait("Category", "Integration")]
public sealed class PlainOutputTests : IDisposable
{
    private const char Escape = '\u001b';

    private readonly string _directory = Directory.CreateTempSubdirectory("loadkit-plain-").FullName;

    [Theory]
    [InlineData("GITHUB_ACTIONS")]
    [InlineData("TF_BUILD")]
    [InlineData("CI")]
    public async Task RedirectedOutput_HasNoAnsiCodes_InCiEnvironments(string ciVariable)
    {
        var scenarioPath = Path.Combine(_directory, "broken.json");
        File.WriteAllText(scenarioPath, """
            {
              "version": 1, "name": "broken", "baseUrl": "http://localhost:5080",
              "load": { "concurrency": 1, "totalRequests": 1, "retries": 3 },
              "requests": [ { "name": "health", "method": "GET", "path": "/health", "expect": { "status": [200] } } ]
            }
            """);

        var result = await CliRunner.RunAsync(
            ["validate", scenarioPath],
            TestContext.Current.CancellationToken,
            environment: new Dictionary<string, string> { [ciVariable] = "true" });

        Assert.DoesNotContain(Escape, result.StandardOutput);
        Assert.Contains("error load.retries (unknown-field)", result.StandardOutput, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }
}
