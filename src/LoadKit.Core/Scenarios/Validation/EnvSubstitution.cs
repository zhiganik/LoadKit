using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;

namespace LoadKit.Core.Scenarios.Validation;

/// <summary>Replaces <c>${env:NAME}</c> in every string value of the scenario, in place, once.</summary>
internal sealed class EnvSubstitution(Func<string, string?> resolveVariable, ICollection<ValidationIssue> issues)
{
    private readonly HashSet<string> _reportedMissingVariables = new(StringComparer.Ordinal);

    public void Apply(JsonObject root)
    {
        foreach (var name in root.Select(property => property.Key).ToList())
        {
            if (name == "$schema")
            {
                continue;
            }

            var childPath = JsonPath.Child(JsonPath.Root, name);
            if (TrySubstituteString(root[name], childPath, out var replacement))
            {
                root[name] = replacement;
            }
            else
            {
                SubstituteInContainer(root[name], childPath);
            }
        }
    }

    private void SubstituteInContainer(JsonNode? node, string path)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                foreach (var name in jsonObject.Select(property => property.Key).ToList())
                {
                    var childPath = JsonPath.Child(path, name);
                    if (TrySubstituteString(jsonObject[name], childPath, out var replacement))
                    {
                        jsonObject[name] = replacement;
                    }
                    else
                    {
                        SubstituteInContainer(jsonObject[name], childPath);
                    }
                }

                break;
            case JsonArray jsonArray:
                for (var index = 0; index < jsonArray.Count; index++)
                {
                    var childPath = JsonPath.Index(path, index);
                    if (TrySubstituteString(jsonArray[index], childPath, out var replacement))
                    {
                        jsonArray[index] = replacement;
                    }
                    else
                    {
                        SubstituteInContainer(jsonArray[index], childPath);
                    }
                }

                break;
        }
    }

    private bool TrySubstituteString(JsonNode? node, string path, [NotNullWhen(true)] out JsonNode? replacement)
    {
        replacement = null;
        if (!JsonNodeReader.IsString(node, out var text) || !EnvReferences.HasReferencePrefix(text))
        {
            return false;
        }

        if (EnvReferences.HasMalformedReference(text))
        {
            issues.Add(ValidationIssue.Error(
                ValidationCodes.InvalidValue,
                path,
                $"{path} contains a malformed env reference",
                "use ${env:NAME} where NAME has letters, digits and underscores"));
        }

        var substituted = EnvReferences.Substitute(text, resolveVariable, variableName =>
        {
            if (_reportedMissingVariables.Add(variableName))
            {
                issues.Add(ValidationIssue.Error(
                    ValidationCodes.EnvMissing,
                    path,
                    $"env variable {variableName} is not set",
                    $"add {variableName}=... to the .env file next to the scenario (for example loadtests/.env) or set it in the environment"));
            }
        });

        replacement = JsonValue.Create(substituted);
        return true;
    }
}
