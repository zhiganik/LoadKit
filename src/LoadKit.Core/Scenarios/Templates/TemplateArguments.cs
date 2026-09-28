using System.Diagnostics.CodeAnalysis;

namespace LoadKit.Core.Scenarios.Templates;

internal static class TemplateArguments
{
    public static bool TryBindWithoutArguments(
        string templateName,
        IReadOnlyList<string> arguments,
        TemplateValueFactory factory,
        [NotNullWhen(true)] out TemplateValueFactory? valueFactory,
        [NotNullWhen(false)] out string? error)
    {
        if (arguments.Count > 0)
        {
            valueFactory = null;
            error = templateName + " takes no arguments";
            return false;
        }

        valueFactory = factory;
        error = null;
        return true;
    }
}
