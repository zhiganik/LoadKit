namespace LoadKit.IntegrationTests;

/// <summary>
/// <c>init</c> without a terminal (flags only), the way AI agents and scripts call it, followed by <c>validate</c>.
/// Interactive prompts need a real terminal and are checked manually.
/// </summary>
[Trait("Category", "Integration")]
public sealed class InitCommandTests : IDisposable
{
    private const int ExitSuccess = 0;
    private const int ExitInvalid = 2;

    private readonly string _directory = Directory.CreateTempSubdirectory("loadkit-init-").FullName;

    [Fact]
    public async Task Flags_CreateLayout_ThatValidatesOnceVariablesAreFilled()
    {
        var init = await RunInDirectoryAsync(["init", "loadtests/scenarios/my-api.json", "--base-url", "http://localhost:5080", "--auth", "apiKey", "--header", "x-api-key"]);

        Assert.True(init.ExitCode == ExitSuccess, init.ToString());
        Assert.Contains("created   loadtests/scenarios/my-api.json", init.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Fill in API_KEY in loadtests/.env", init.StandardOutput, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(_directory, "loadtests", "scenario.schema.json")));
        Assert.Contains("loadtests/reports/", File.ReadAllText(Path.Combine(_directory, ".gitignore")), StringComparison.Ordinal);

        var beforeFilling = await RunInDirectoryAsync(["validate", "loadtests/scenarios/my-api.json"]);
        File.AppendAllText(Path.Combine(_directory, "loadtests", ".env"), "API_KEY=dev-api-key\n");
        var afterFilling = await RunInDirectoryAsync(["validate", "loadtests/scenarios/my-api.json"]);

        Assert.True(beforeFilling.ExitCode == ExitInvalid, beforeFilling.ToString());
        Assert.Contains("env variable API_KEY is not set", beforeFilling.StandardOutput, StringComparison.Ordinal);
        Assert.True(afterFilling.ExitCode == ExitSuccess, afterFilling.ToString());
    }

    [Fact]
    public async Task ExistingScenario_IsNotOverwritten()
    {
        await RunInDirectoryAsync(["init", "loadtests/scenarios/api.json", "--base-url", "http://localhost:5080"]);
        File.WriteAllText(Path.Combine(_directory, "loadtests", "scenarios", "api.json"), "edited");

        var second = await RunInDirectoryAsync(["init", "loadtests/scenarios/api.json", "--base-url", "http://localhost:5080"]);

        Assert.True(second.ExitCode == ExitInvalid, second.ToString());
        Assert.Contains("already exists", second.StandardOutput, StringComparison.Ordinal);
        Assert.Equal("edited", File.ReadAllText(Path.Combine(_directory, "loadtests", "scenarios", "api.json")));
    }

    [Theory]
    [InlineData(new[] { "init", "loadtests/scenarios/api.json" }, "--base-url is required")]
    [InlineData(new[] { "init", "loadtests/scenarios/api.json", "--base-url", "http://localhost", "--auth", "azureIdentity" }, "--scope is required")]
    public async Task MissingAnswers_ExitTwo_WithHint(string[] arguments, string expectedMessage)
    {
        var result = await RunInDirectoryAsync(arguments);

        Assert.True(result.ExitCode == ExitInvalid, result.ToString());
        Assert.Contains(expectedMessage, result.StandardOutput, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(_directory, "loadtests")));
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    private Task<CliResult> RunInDirectoryAsync(string[] arguments)
    {
        return CliRunner.RunAsync(arguments, TestContext.Current.CancellationToken, _directory);
    }
}
