using System.Text.RegularExpressions;

namespace LoadKit.IntegrationTests;

/// <summary>
/// A short token lifetime over a longer run: background refresh at ~80% of the lifetime must keep every request
/// authorized. TESTING.md describes 10 s tokens in a 30 s run; 3 s in 8 s gives the same two-plus refreshes faster.
/// </summary>
[Trait("Category", "Integration")]
public sealed partial class TokenRefreshTests(ShortTokenLifetimeTargetApiFixture targetApi)
    : IClassFixture<ShortTokenLifetimeTargetApiFixture>, IDisposable
{
    private const int RunSeconds = 8;

    private readonly string _directory = Directory.CreateTempSubdirectory("loadkit-refresh-").FullName;

    [Fact]
    public async Task Login_TokenOutlivedByRun_IsRefreshed_WithoutUnauthorizedResponses()
    {
        File.WriteAllText(Path.Combine(_directory, ".env"), "LOADKIT_IT_USER=loadtest@example.com\nLOADKIT_IT_PASSWORD=dev-password\n");
        var scenarioPath = Path.Combine(_directory, "login.json");
        File.WriteAllText(scenarioPath, $$"""
            {
              "version": 1, "name": "login-refresh", "baseUrl": "{{targetApi.BaseUrl}}",
              "auth": {
                "type": "login",
                "request": { "method": "POST", "path": "/auth/login", "body": { "email": "${env:LOADKIT_IT_USER}", "password": "${env:LOADKIT_IT_PASSWORD}" } },
                "tokenPath": "$.accessToken", "expiresInPath": "$.expiresIn"
              },
              "load": { "concurrency": 4, "durationSec": {{RunSeconds}} },
              "requests": [ { "name": "secure", "method": "GET", "path": "/secure", "expect": { "status": [200] } } ],
              "thresholds": { "errorRatePercent": 0, "p99Ms": 1000 }
            }
            """);

        var result = await CliRunner.RunAsync(["run", scenarioPath], TestContext.Current.CancellationToken);

        Assert.True(result.ExitCode == 0, result.ToString());
        Assert.Contains(
            $"login: token acquired, expires in {ShortTokenLifetimeTargetApiFixture.TokenLifetimeSeconds} s",
            result.StandardOutput,
            StringComparison.Ordinal);
        Assert.DoesNotMatch(UnauthorizedRow(), result.StandardOutput);
        Assert.DoesNotContain("token refresh failed", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("dev-password", result.StandardOutput, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    [GeneratedRegex(@"^\| 401 ", RegexOptions.Multiline)]
    private static partial Regex UnauthorizedRow();
}
