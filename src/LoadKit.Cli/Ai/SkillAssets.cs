namespace LoadKit.Cli.Ai;

/// <summary>The <c>loadtest</c> skill embedded from <c>ai/skills/loadtest/**</c> (see LoadKit.Cli.csproj).</summary>
internal static class SkillAssets
{
    public const string SkillName = "loadtest";
    public const string SkillFileName = "SKILL.md";
    private const string ResourcePrefix = "LoadKit.AiAssets/loadtest/";

    /// <summary>Relative path inside the skill folder (with '/') → file content.</summary>
    public static IReadOnlyDictionary<string, string> ReadAll()
    {
        var assembly = typeof(SkillAssets).Assembly;
        var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!resourceName.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(resourceName)!;
            using var reader = new StreamReader(stream);
            files[resourceName[ResourcePrefix.Length..].Replace('\\', '/')] = reader.ReadToEnd();
        }

        if (!files.ContainsKey(SkillFileName))
        {
            throw new InvalidOperationException($"The embedded skill has no {SkillFileName}; check LoadKit.Cli.csproj.");
        }

        return files;
    }
}
