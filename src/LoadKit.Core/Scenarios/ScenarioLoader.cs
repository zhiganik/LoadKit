using LoadKit.Core.Scenarios.Templates;
using LoadKit.Core.Scenarios.Validation;

namespace LoadKit.Core.Scenarios;

/// <summary>
/// Reads a scenario file, loads <c>.env</c>, validates and compiles. Process environment variables
/// take precedence over <c>.env</c> values.
/// </summary>
public sealed class ScenarioLoader(TemplateRegistry templateRegistry, Func<string, string?> getProcessVariable)
{
    public ScenarioLoader()
        : this(TemplateRegistry.CreateDefault(), Environment.GetEnvironmentVariable)
    {
    }

    /// <param name="envFilePath">Explicit <c>.env</c>; when null, <see cref="EnvFileLoader.FindDefault"/> is used.</param>
    public async Task<ScenarioLoadResult> LoadAsync(string scenarioFilePath, string? envFilePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(scenarioFilePath))
        {
            return Failed(ValidationIssue.Error(
                ValidationCodes.FileNotFound,
                JsonPath.Root,
                $"scenario file not found: {scenarioFilePath}",
                "check the path; scenarios usually live in loadtests/scenarios/"));
        }

        if (envFilePath is not null && !File.Exists(envFilePath))
        {
            return Failed(ValidationIssue.Error(
                ValidationCodes.EnvFileNotFound,
                JsonPath.Root,
                $"env file not found: {envFilePath}",
                "check the --env-file path or omit it to use .env next to the scenario"));
        }

        var effectiveEnvFilePath = envFilePath ?? EnvFileLoader.FindDefault(scenarioFilePath);
        var envFileVariables = effectiveEnvFilePath is null
            ? new Dictionary<string, string>()
            : await EnvFileLoader.LoadAsync(effectiveEnvFilePath, cancellationToken);
        var scenarioJson = await File.ReadAllTextAsync(scenarioFilePath, cancellationToken);
        return LoadFromJson(scenarioJson, envFileVariables);
    }

    public ScenarioLoadResult LoadFromJson(string scenarioJson, IReadOnlyDictionary<string, string> envFileVariables)
    {
        var parseIssues = new List<ValidationIssue>();
        var root = ScenarioJsonParser.Parse(scenarioJson, parseIssues);
        if (root is null)
        {
            return new ScenarioLoadResult(null, parseIssues);
        }

        var issues = new ScenarioValidator(templateRegistry).Validate(root, variableName => ResolveVariable(variableName, envFileVariables));
        if (issues.Any(issue => issue.Severity == ValidationSeverity.Error))
        {
            return new ScenarioLoadResult(null, issues);
        }

        var scenario = ScenarioBinder.Bind(root);
        var compiledScenario = new ScenarioCompiler(new TemplateCompiler(templateRegistry)).Compile(scenario);
        return new ScenarioLoadResult(compiledScenario, issues);
    }

    private string? ResolveVariable(string variableName, IReadOnlyDictionary<string, string> envFileVariables)
    {
        var processValue = getProcessVariable(variableName);
        if (!string.IsNullOrEmpty(processValue))
        {
            return processValue;
        }

        return envFileVariables.TryGetValue(variableName, out var fileValue) ? fileValue : null;
    }

    private static ScenarioLoadResult Failed(ValidationIssue issue)
    {
        return new ScenarioLoadResult(null, [issue]);
    }
}
