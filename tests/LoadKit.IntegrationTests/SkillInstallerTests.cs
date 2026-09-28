using LoadKit.Cli.Ai;

namespace LoadKit.IntegrationTests;

/// <summary>
/// <c>ai install</c> / <c>ai status</c> logic with a temporary home folder, so tests never touch the real ~/.claude.
/// </summary>
public sealed class SkillInstallerTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("loadkit-skill-").FullName;

    private string WorkingDirectory => Path.Combine(_root, "project");

    private string HomeDirectory => Path.Combine(_root, "home");

    [Fact]
    public void EmbeddedSkill_MatchesRepositoryFiles()
    {
        var repositorySkill = Path.Combine(FindRepositoryRoot(), "ai", "skills", "loadtest");
        var expected = Directory.GetFiles(repositorySkill, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(repositorySkill, path).Replace('\\', '/'), File.ReadAllText);

        Assert.Equal(expected, SkillAssets.ReadAll());
    }

    [Fact]
    [Trait("Category", "Docs")]
    public void SkillVersion_MatchesToolVersion()
    {
        var embeddedVersion = SkillVersion.Read(SkillAssets.ReadAll()[SkillAssets.SkillFileName]);

        Assert.Equal(SkillVersion.ToolMajorMinor(), embeddedVersion);
    }

    [Fact]
    public void Install_Global_WritesIntoHome_AndIsIdempotent()
    {
        var installer = CreateInstaller();

        var first = installer.Install(installer.GlobalSkillDirectory, SkillAssets.ReadAll());
        var second = installer.Install(installer.GlobalSkillDirectory, SkillAssets.ReadAll());

        Assert.All(first, change => Assert.Equal(FileChange.Created, change.Change));
        Assert.All(second, change => Assert.Equal(FileChange.Unchanged, change.Change));
        Assert.True(File.Exists(Path.Combine(HomeDirectory, ".claude", "skills", "loadtest", "SKILL.md")));
        Assert.Equal(SkillVersion.ToolMajorMinor(), installer.Inspect(installer.GlobalSkillDirectory)!.Version);
    }

    [Fact]
    public void Install_OverwritesChangedFiles_AndKeepsForeignOnes()
    {
        var installer = CreateInstaller();
        var skillDirectory = installer.CustomSkillDirectory("tools/skills");
        installer.Install(skillDirectory, SkillAssets.ReadAll());
        File.WriteAllText(Path.Combine(skillDirectory, "SKILL.md"), "edited");
        File.WriteAllText(Path.Combine(skillDirectory, "NOTES.md"), "mine");

        var changes = installer.Install(skillDirectory, SkillAssets.ReadAll());

        Assert.Contains(new FileChange("tools/skills/loadtest/SKILL.md", FileChange.Updated), changes);
        Assert.Equal("mine", File.ReadAllText(Path.Combine(skillDirectory, "NOTES.md")));
    }

    [Theory]
    [InlineData("metadata:\n  loadkit-version: \"0.9\"\n", true, false)]
    [InlineData("metadata:\n  loadkit-version: \"1.0\"\n", false, false)]
    [InlineData("metadata:\n  loadkit-version: 2.1\n", false, true)]
    [InlineData("no version here", true, false)]
    public void Inspect_ComparesMajorMinorWithTool(string skillContent, bool outdated, bool newer)
    {
        var installer = CreateInstaller();
        Directory.CreateDirectory(installer.ProjectSkillDirectory);
        File.WriteAllText(Path.Combine(installer.ProjectSkillDirectory, "SKILL.md"), skillContent);

        var installed = installer.Inspect(installer.ProjectSkillDirectory)!;

        Assert.Equal((outdated, newer), (installed.IsOutdated, installed.IsNewerThanTool));
    }

    [Fact]
    public void AgentsMd_BlockIsAppendedOnce_AndReplacedInPlace()
    {
        var installer = CreateInstaller();
        Directory.CreateDirectory(WorkingDirectory);
        File.WriteAllText(installer.AgentsMdPath, "# Project rules\n\nKeep it simple.\n");

        installer.UpdateAgentsMd(installer.ProjectSkillDirectory);
        var afterFirst = File.ReadAllText(installer.AgentsMdPath);
        File.WriteAllText(installer.AgentsMdPath, afterFirst.Replace("never write code", "OLD TEXT", StringComparison.Ordinal) + "\n## After\n");
        installer.UpdateAgentsMd(installer.ProjectSkillDirectory);
        var afterSecond = File.ReadAllText(installer.AgentsMdPath);

        Assert.StartsWith("# Project rules\n\nKeep it simple.\n\n<!-- loadkit:start -->", afterFirst, StringComparison.Ordinal);
        Assert.Contains("[.claude/skills/loadtest/SKILL.md](.claude/skills/loadtest/SKILL.md)", afterFirst, StringComparison.Ordinal);
        Assert.Single(afterSecond.Split(AgentsMdBlock.StartMarker)[1..]);
        Assert.DoesNotContain("OLD TEXT", afterSecond, StringComparison.Ordinal);
        Assert.EndsWith("<!-- loadkit:end -->\n\n## After\n", afterSecond, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentsMd_ForGlobalSkill_LinksAbsolutePath()
    {
        var installer = CreateInstaller();

        installer.UpdateAgentsMd(installer.GlobalSkillDirectory);

        Assert.Contains(
            Path.Combine(HomeDirectory, ".claude", "skills", "loadtest", "SKILL.md").Replace('\\', '/'),
            File.ReadAllText(installer.AgentsMdPath),
            StringComparison.Ordinal);
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }

    private SkillInstaller CreateInstaller()
    {
        return new SkillInstaller(WorkingDirectory, HomeDirectory);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LoadKit.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("LoadKit.slnx not found.");
    }
}
