using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace LoadKit.Core.Scenarios.Templates;

/// <summary><c>{{randomInt:MIN:MAX}}</c>, both bounds inclusive.</summary>
public sealed class RandomIntTemplateGenerator : ITemplateGenerator
{
    public string Name => "randomInt";

    public string Usage => "{{randomInt:MIN:MAX}}";

    public bool IsNumeric => true;

    public bool TryBind(
        IReadOnlyList<string> arguments,
        [NotNullWhen(true)] out TemplateValueFactory? valueFactory,
        [NotNullWhen(false)] out string? error)
    {
        valueFactory = null;
        if (arguments.Count != 2)
        {
            error = "randomInt needs two arguments: " + Usage;
            return false;
        }

        if (!TryParseInteger(arguments[0], out var minimum) || !TryParseInteger(arguments[1], out var maximum))
        {
            error = "randomInt bounds must be integers: " + Usage;
            return false;
        }

        if (minimum > maximum)
        {
            error = "randomInt MIN must not be greater than MAX";
            return false;
        }

        var exclusiveMaximum = (long)maximum + 1;
        valueFactory = context => context.Random.NextInt64(minimum, exclusiveMaximum).ToString(CultureInfo.InvariantCulture);
        error = null;
        return true;
    }

    private static bool TryParseInteger(string text, out int value)
    {
        return int.TryParse(text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
    }
}
