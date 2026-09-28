using System.Text.Json.Nodes;
using LoadKit.Core.Scenarios.Templates;
using LoadKit.Core.Scenarios.Validation;

namespace LoadKit.Core.Scenarios;

/// <summary>
/// The source of truth for the scenario format (no JSON Schema library). Collects all issues with JSON paths.
/// Order: version → structure → secret literals (raw values) → <c>${env:}</c> substitution → semantic rules.
/// </summary>
public sealed class ScenarioValidator(TemplateRegistry templateRegistry)
{
    /// <summary>
    /// Validates <paramref name="root"/> and substitutes <c>${env:}</c> values in it, in place.
    /// </summary>
    public IReadOnlyList<ValidationIssue> Validate(JsonObject root, Func<string, string?> resolveVariable)
    {
        var issues = new List<ValidationIssue>();
        if (!IsSupportedVersion(root, issues))
        {
            return issues;
        }

        new StructureValidator(issues).ValidateObject(root, ScenarioFormat.Root, JsonPath.Root);
        new SecretLiteralValidator(issues).Validate(root);
        new EnvSubstitution(resolveVariable, issues).Apply(root);
        new SemanticValidator(issues, new TemplateCompiler(templateRegistry)).Validate(root);
        return issues;
    }

    private static bool IsSupportedVersion(JsonObject root, List<ValidationIssue> issues)
    {
        if (!JsonNodeReader.TryGetInteger(root, "version", out var version) || version == ScenarioFormat.CurrentVersion)
        {
            // Missing or mistyped version is reported by the structural check.
            return true;
        }

        issues.Add(ValidationIssue.Error(
            ValidationCodes.UnsupportedVersion,
            "version",
            $"scenario format version {version} is not supported by this LoadKit (supported: {ScenarioFormat.CurrentVersion})",
            "update the tool: dotnet tool update -g LoadKit"));
        return false;
    }
}
