namespace LoadKit.Core.Scenarios;

/// <summary>
/// Reads <c>.env</c> files: <c>KEY=VALUE</c> lines, <c>#</c> comments, optional quotes and <c>export </c> prefix.
/// </summary>
public static class EnvFileLoader
{
    public const string EnvFileName = ".env";

    private const string ExportPrefix = "export ";

    public static async Task<IReadOnlyDictionary<string, string>> LoadAsync(string envFilePath, CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(envFilePath, cancellationToken);
        return Parse(content);
    }

    public static IReadOnlyDictionary<string, string> Parse(string content)
    {
        var variables = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var rawLine in content.TrimStart('﻿').Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith(ExportPrefix, StringComparison.Ordinal))
            {
                line = line[ExportPrefix.Length..].TrimStart();
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var name = line[..separatorIndex].Trim();
            var value = Unquote(line[(separatorIndex + 1)..].Trim());
            variables[name] = value;
        }

        return variables;
    }

    /// <summary>
    /// The <c>.env</c> used when none is given: next to the scenario, else in its parent folder
    /// (<c>loadtests/.env</c> for <c>loadtests/scenarios/x.json</c>). Null when neither exists.
    /// </summary>
    public static string? FindDefault(string scenarioFilePath)
    {
        var scenarioDirectory = Path.GetDirectoryName(Path.GetFullPath(scenarioFilePath));
        if (scenarioDirectory is null)
        {
            return null;
        }

        var besideScenario = Path.Combine(scenarioDirectory, EnvFileName);
        if (File.Exists(besideScenario))
        {
            return besideScenario;
        }

        var parentDirectory = Path.GetDirectoryName(scenarioDirectory);
        var inParent = parentDirectory is null ? null : Path.Combine(parentDirectory, EnvFileName);
        return inParent is not null && File.Exists(inParent) ? inParent : null;
    }

    private static string Unquote(string value)
    {
        var isQuoted = value.Length >= 2
            && (value[0] == '"' || value[0] == '\'')
            && value[^1] == value[0];
        return isQuoted ? value[1..^1] : value;
    }
}
