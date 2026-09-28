using System.Text.RegularExpressions;

namespace LoadKit.Core.Scenarios.Validation;

/// <summary><c>${env:NAME}</c> references: NAME starts with a letter or underscore, then letters, digits, underscores.</summary>
internal static partial class EnvReferences
{
    private const string ReferencePrefix = "${env:";

    public static bool ContainsReference(string value)
    {
        return ReferencePattern().IsMatch(value);
    }

    public static bool HasReferencePrefix(string value)
    {
        return value.Contains(ReferencePrefix, StringComparison.Ordinal);
    }

    /// <summary>True when some <c>${env:</c> is not followed by a valid name and <c>}</c>.</summary>
    public static bool HasMalformedReference(string value)
    {
        var prefixCount = 0;
        var position = 0;
        while ((position = value.IndexOf(ReferencePrefix, position, StringComparison.Ordinal)) >= 0)
        {
            prefixCount++;
            position += ReferencePrefix.Length;
        }

        return prefixCount != ReferencePattern().Count(value);
    }

    /// <summary>Replaces each reference once (values are not substituted again). Unset or empty variables stay as-is.</summary>
    public static string Substitute(string value, Func<string, string?> resolveVariable, Action<string> onMissingVariable)
    {
        return ReferencePattern().Replace(value, match =>
        {
            var variableName = match.Groups[1].Value;
            var variableValue = resolveVariable(variableName);
            if (string.IsNullOrEmpty(variableValue))
            {
                onMissingVariable(variableName);
                return match.Value;
            }

            return variableValue;
        });
    }

    [GeneratedRegex(@"\$\{env:([A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.CultureInvariant)]
    private static partial Regex ReferencePattern();
}
