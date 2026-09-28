namespace LoadKit.IntegrationTests;

/// <summary>Command descriptions are Spectre markup: a stray '[' breaks --help at runtime, not at build time.</summary>
[Trait("Category", "Integration")]
public sealed class HelpTests
{
    [Fact]
    public async Task Help_ListsAllCommands()
    {
        var result = await CliRunner.RunAsync(["--help"], TestContext.Current.CancellationToken);

        Assert.True(result.ExitCode == 0, result.ToString());
        Assert.DoesNotContain("Error:", result.StandardOutput, StringComparison.Ordinal);
        foreach (var command in new[] { "validate", "check", "run" })
        {
            Assert.Contains(command, result.StandardOutput, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("validate")]
    [InlineData("check")]
    [InlineData("run")]
    public async Task CommandHelp_Renders(string command)
    {
        var result = await CliRunner.RunAsync([command, "--help"], TestContext.Current.CancellationToken);

        Assert.True(result.ExitCode == 0, result.ToString());
        Assert.Contains("<file>", result.StandardOutput, StringComparison.Ordinal);
    }
}
