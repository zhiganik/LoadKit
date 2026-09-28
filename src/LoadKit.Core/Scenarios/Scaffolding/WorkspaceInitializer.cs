using System.Text;

namespace LoadKit.Core.Scenarios.Scaffolding;

/// <summary>
/// Creates the <c>loadtests/</c> layout: the scenario, <c>scenario.schema.json</c>, <c>.env</c> with empty variables and
/// <c>.gitignore</c> entries. Never overwrites a scenario or an existing <c>.env</c> value; safe to run again.
/// </summary>
/// <param name="workingDirectory">Relative paths in options and results are relative to it.</param>
public sealed class WorkspaceInitializer(string workingDirectory)
{
    public const string SchemaFileName = "scenario.schema.json";
    public const string EnvFileName = ".env";
    public const string ReportsFolderName = "reports";
    public const string ScenariosFolderName = "scenarios";

    private const string Created = "created";
    private const string Updated = "updated";
    private const string Unchanged = "unchanged";
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>File-system checks; <see cref="ScenarioScaffolder.Validate"/> covers the answers themselves.</summary>
    public IReadOnlyList<ValidationIssue> CheckTarget(InitOptions options)
    {
        var scenarioPath = FullPath(options.ScenarioPath);
        return File.Exists(scenarioPath)
            ? [ValidationIssue.Error(ValidationCodes.InvalidValue, "<file>", $"{Relative(scenarioPath)} already exists", "choose another file name; init never overwrites a scenario")]
            : [];
    }

    /// <summary>
    /// The workspace root is the folder above <c>scenarios/</c> (<c>loadtests/scenarios/x.json</c> → <c>loadtests/</c>),
    /// otherwise the scenario's own folder.
    /// </summary>
    public string WorkspaceRoot(InitOptions options)
    {
        var scenarioDirectory = Path.GetDirectoryName(FullPath(options.ScenarioPath))!;
        return Path.GetFileName(scenarioDirectory).Equals(ScenariosFolderName, StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(scenarioDirectory)!
            : scenarioDirectory;
    }

    /// <param name="scenarioSchemaJson">Content of <c>schemas/scenario.schema.json</c> shipped with the tool.</param>
    public async Task<InitResult> InitializeAsync(InitOptions options, string scenarioSchemaJson, CancellationToken cancellationToken)
    {
        var scenarioPath = FullPath(options.ScenarioPath);
        var root = WorkspaceRoot(options);
        var changes = new List<InitFileChange>();

        var schemaPath = Path.Combine(root, SchemaFileName);
        changes.Add(await WriteIfDifferentAsync(schemaPath, scenarioSchemaJson, cancellationToken));

        Directory.CreateDirectory(Path.GetDirectoryName(scenarioPath)!);
        var schemaReference = Path.GetRelativePath(Path.GetDirectoryName(scenarioPath)!, schemaPath).Replace('\\', '/');
        await File.WriteAllTextAsync(scenarioPath, ScenarioScaffolder.CreateScenarioJson(options, schemaReference), Utf8WithoutBom, cancellationToken);
        changes.Add(new InitFileChange(Relative(scenarioPath), Created));

        var envFilePath = Path.Combine(root, EnvFileName);
        var variables = ScenarioScaffolder.EnvVariables(options.AuthType);
        var (envChange, emptyVariables) = await EnsureEnvVariablesAsync(envFilePath, variables, cancellationToken);
        changes.Add(envChange);

        var reportsPath = Path.Combine(root, ReportsFolderName);
        if (await EnsureGitIgnoreAsync(root, envFilePath, reportsPath, cancellationToken) is { } gitIgnoreChange)
        {
            changes.Add(gitIgnoreChange);
        }

        return new InitResult(changes, Relative(scenarioPath), Relative(envFilePath), Relative(reportsPath) + "/", emptyVariables);
    }

    private async Task<InitFileChange> WriteIfDifferentAsync(string path, string content, CancellationToken cancellationToken)
    {
        var exists = File.Exists(path);
        if (exists && await File.ReadAllTextAsync(path, cancellationToken) == content)
        {
            return new InitFileChange(Relative(path), Unchanged);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content, Utf8WithoutBom, cancellationToken);
        return new InitFileChange(Relative(path), exists ? Updated : Created);
    }

    // Values are only checked for emptiness, never returned: .env holds secrets.
    private async Task<(InitFileChange Change, IReadOnlyList<string> EmptyVariables)> EnsureEnvVariablesAsync(
        string envFilePath,
        IReadOnlyList<string> variables,
        CancellationToken cancellationToken)
    {
        var exists = File.Exists(envFilePath);
        var existingContent = exists ? await File.ReadAllTextAsync(envFilePath, cancellationToken) : string.Empty;
        var existingValues = EnvFileLoader.Parse(existingContent);
        var missing = variables.Where(name => !existingValues.ContainsKey(name)).ToList();
        var empty = variables.Where(name => !existingValues.TryGetValue(name, out var value) || value.Length == 0).ToList();

        if (exists && missing.Count == 0)
        {
            return (new InitFileChange(Relative(envFilePath), Unchanged), empty);
        }

        var addition = new StringBuilder();
        if (!exists)
        {
            addition.Append("# Secrets for LoadKit scenarios: values only here, never in scenario files. Do not commit.\n");
        }
        else if (existingContent.Length > 0 && !existingContent.EndsWith('\n'))
        {
            addition.Append('\n');
        }

        foreach (var name in missing)
        {
            addition.Append(name).Append("=\n");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(envFilePath)!);
        await File.AppendAllTextAsync(envFilePath, addition.ToString(), Utf8WithoutBom, cancellationToken);
        return (new InitFileChange(Relative(envFilePath), exists ? Updated : Created), empty);
    }

    /// <summary>Adds the .env and reports entries to the repository's .gitignore (or the working directory's).</summary>
    private async Task<InitFileChange?> EnsureGitIgnoreAsync(string root, string envFilePath, string reportsPath, CancellationToken cancellationToken)
    {
        var gitIgnoreDirectory = FindGitRoot(root) ?? workingDirectory;
        var entries = new[]
        {
            Path.GetRelativePath(gitIgnoreDirectory, envFilePath).Replace('\\', '/'),
            Path.GetRelativePath(gitIgnoreDirectory, reportsPath).Replace('\\', '/') + "/",
        };
        if (entries.Any(entry => entry.StartsWith("..", StringComparison.Ordinal)))
        {
            return null;
        }

        var gitIgnorePath = Path.Combine(gitIgnoreDirectory, ".gitignore");
        var exists = File.Exists(gitIgnorePath);
        var content = exists ? await File.ReadAllTextAsync(gitIgnorePath, cancellationToken) : string.Empty;
        var existingLines = new HashSet<string>(content.Split('\n').Select(line => line.Trim()), StringComparer.Ordinal);
        var missing = entries.Where(entry => !existingLines.Contains(entry) && !existingLines.Contains("/" + entry)).ToList();
        if (missing.Count == 0)
        {
            return new InitFileChange(Relative(gitIgnorePath), Unchanged);
        }

        var addition = new StringBuilder();
        if (content.Length > 0)
        {
            addition.Append(content.EndsWith('\n') ? "\n" : "\n\n");
        }

        addition.Append("# LoadKit: secrets and run reports\n");
        foreach (var entry in missing)
        {
            addition.Append(entry).Append('\n');
        }

        await File.AppendAllTextAsync(gitIgnorePath, addition.ToString(), Utf8WithoutBom, cancellationToken);
        return new InitFileChange(Relative(gitIgnorePath), exists ? Updated : Created);
    }

    private static string? FindGitRoot(string startDirectory)
    {
        for (var directory = new DirectoryInfo(startDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")) || File.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }
        }

        return null;
    }

    private string FullPath(string path)
    {
        return Path.GetFullPath(path, workingDirectory);
    }

    private string Relative(string fullPath)
    {
        return Path.GetRelativePath(workingDirectory, fullPath).Replace('\\', '/');
    }
}
