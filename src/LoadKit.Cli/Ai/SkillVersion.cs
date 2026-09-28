using System.Text.RegularExpressions;

namespace LoadKit.Cli.Ai;

/// <summary>
/// The <c>metadata.loadkit-version</c> of a SKILL.md (major.minor) compared with the tool version. Patch releases do not
/// change the skill, so only major.minor matters.
/// </summary>
internal static partial class SkillVersion
{
    public static Version? Read(string skillMarkdown)
    {
        var match = LoadKitVersionLine().Match(skillMarkdown);
        return match.Success && Version.TryParse(match.Groups["version"].Value, out var version) ? MajorMinor(version) : null;
    }

    public static Version ToolMajorMinor()
    {
        return MajorMinor(Version.Parse(ToolInfo.Version.Split('-')[0]));
    }

    public static Version MajorMinor(Version version)
    {
        return new Version(version.Major, version.Minor);
    }

    [GeneratedRegex("""^\s*loadkit-version:\s*"?(?<version>\d+\.\d+(\.\d+)?)"?\s*$""", RegexOptions.Multiline)]
    private static partial Regex LoadKitVersionLine();
}
