namespace LoadKit.Cli.Ai;

/// <param name="Version">major.minor from <c>metadata.loadkit-version</c>; null if the file has none.</param>
internal sealed record InstalledSkill(string Directory, Version? Version)
{
    /// <summary>Older than the tool, or without a version: the user should run <c>loadtest ai install</c> again.</summary>
    public bool IsOutdated => Version is null || Version < SkillVersion.ToolMajorMinor();

    public bool IsNewerThanTool => Version is not null && Version > SkillVersion.ToolMajorMinor();
}
