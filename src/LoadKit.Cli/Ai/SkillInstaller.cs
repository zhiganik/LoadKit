using System.Text;

namespace LoadKit.Cli.Ai;

/// <summary>
/// Writes the embedded skill into a skills folder and inspects installed copies. The folder <c>loadtest/</c> belongs to
/// LoadKit: its files are overwritten with the tool's version; other files there are left alone.
/// </summary>
/// <param name="homeDirectory">The user's home, for <c>--global</c>; tests pass a temporary folder.</param>
internal sealed class SkillInstaller(string workingDirectory, string homeDirectory)
{
    public const string AgentsMdFileName = "AGENTS.md";
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    public string ProjectSkillDirectory => Path.Combine(workingDirectory, ".claude", "skills", SkillAssets.SkillName);

    public string GlobalSkillDirectory => Path.Combine(homeDirectory, ".claude", "skills", SkillAssets.SkillName);

    public string AgentsMdPath => Path.Combine(workingDirectory, AgentsMdFileName);

    /// <summary><c>--dir &lt;path&gt;</c>: the skill goes to <c>&lt;path&gt;/loadtest/</c>.</summary>
    public string CustomSkillDirectory(string skillsDirectory)
    {
        return Path.Combine(Path.GetFullPath(skillsDirectory, workingDirectory), SkillAssets.SkillName);
    }

    public IReadOnlyList<FileChange> Install(string skillDirectory, IReadOnlyDictionary<string, string> files)
    {
        var changes = new List<FileChange>(files.Count);
        foreach (var (relativePath, content) in files)
        {
            var path = Path.Combine(skillDirectory, relativePath);
            changes.Add(WriteIfDifferent(path, content));
        }

        return changes;
    }

    /// <param name="skillDirectory">Where the skill was installed; the block links to its SKILL.md.</param>
    public FileChange UpdateAgentsMd(string skillDirectory)
    {
        var skillFile = Path.Combine(skillDirectory, SkillAssets.SkillFileName);
        var link = IsUnder(skillFile, workingDirectory)
            ? Path.GetRelativePath(workingDirectory, skillFile).Replace('\\', '/')
            : skillFile.Replace('\\', '/');
        var existing = File.Exists(AgentsMdPath) ? File.ReadAllText(AgentsMdPath) : string.Empty;
        return WriteIfDifferent(AgentsMdPath, AgentsMdBlock.Merge(existing, AgentsMdBlock.Build(link)));
    }

    /// <returns>Null when there is no SKILL.md in the folder.</returns>
    public InstalledSkill? Inspect(string skillDirectory)
    {
        var skillFile = Path.Combine(skillDirectory, SkillAssets.SkillFileName);
        return File.Exists(skillFile) ? new InstalledSkill(skillDirectory, SkillVersion.Read(File.ReadAllText(skillFile))) : null;
    }

    public bool AgentsMdHasBlock()
    {
        return File.Exists(AgentsMdPath) && File.ReadAllText(AgentsMdPath).Contains(AgentsMdBlock.StartMarker, StringComparison.Ordinal);
    }

    /// <summary>A path relative to the working directory when it is inside it, otherwise absolute.</summary>
    public string Display(string path)
    {
        return IsUnder(path, workingDirectory) ? Path.GetRelativePath(workingDirectory, path).Replace('\\', '/') : path;
    }

    private FileChange WriteIfDifferent(string path, string content)
    {
        var exists = File.Exists(path);
        if (exists && File.ReadAllText(path) == content)
        {
            return new FileChange(Display(path), FileChange.Unchanged);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, Utf8WithoutBom);
        return new FileChange(Display(path), exists ? FileChange.Updated : FileChange.Created);
    }

    private static bool IsUnder(string path, string directory)
    {
        var relative = Path.GetRelativePath(directory, path);
        return !relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative);
    }
}
