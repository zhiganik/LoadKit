namespace LoadKit.IntegrationTests;

/// <summary><c>ai install</c>, <c>ai status</c> and the outdated-skill warning of <c>validate</c>, as real processes.</summary>
[Trait("Category", "Integration")]
public sealed class AiCommandTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("loadkit-ai-").FullName;

    [Fact]
    public async Task Install_IntoProject_ThenStatusIsUpToDate()
    {
        var install = await RunAsync("ai", "install", "--agents-md");
        var status = await RunAsync("ai", "status");

        Assert.True(install.ExitCode == 0, install.ToString());
        Assert.Contains("created   .claude/skills/loadtest/SKILL.md", install.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("created   AGENTS.md", install.StandardOutput, StringComparison.Ordinal);
        Assert.True(status.ExitCode == 0, status.ToString());
        Assert.Contains("project: 1.0, up to date (.claude/skills/loadtest)", status.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("AGENTS.md: has the LoadKit block", status.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Install_IntoDirectory()
    {
        var install = await RunAsync("ai", "install", "--dir", "agent-skills");

        Assert.True(install.ExitCode == 0, install.ToString());
        Assert.True(File.Exists(Path.Combine(_directory, "agent-skills", "loadtest", "SCENARIO_REFERENCE.md")));
    }

    [Fact]
    public async Task Install_GlobalAndDir_IsRejected()
    {
        var install = await RunAsync("ai", "install", "--global", "--dir", "x");

        Assert.NotEqual(0, install.ExitCode);
        Assert.Contains("either --global or --dir", install.StandardOutput + install.StandardError, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(_directory, "x")));
    }

    [Fact]
    public async Task Validate_WarnsAboutOutdatedProjectSkill_WithoutChangingExitCode()
    {
        var skillDirectory = Path.Combine(_directory, ".claude", "skills", "loadtest");
        Directory.CreateDirectory(skillDirectory);
        File.WriteAllText(Path.Combine(skillDirectory, "SKILL.md"), "---\nname: loadtest\nmetadata:\n  loadkit-version: \"0.9\"\n---\n");
        File.WriteAllText(Path.Combine(_directory, "smoke.json"), """
            {
              "version": 1, "name": "smoke", "baseUrl": "http://localhost:5080",
              "load": { "concurrency": 1, "totalRequests": 1 },
              "requests": [ { "name": "health", "method": "GET", "path": "/health", "expect": { "status": [200] } } ]
            }
            """);

        var validate = await RunAsync("validate", "smoke.json");
        var status = await RunAsync("ai", "status");

        Assert.True(validate.ExitCode == 0, validate.ToString());
        Assert.Contains("warning (skill-outdated): the loadtest skill in .claude/skills/loadtest is 0.9, the tool is 1.0", validate.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("project: 0.9, older than the tool", status.StandardOutput, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    private Task<CliResult> RunAsync(params string[] arguments)
    {
        return CliRunner.RunAsync(arguments, TestContext.Current.CancellationToken, _directory);
    }
}
