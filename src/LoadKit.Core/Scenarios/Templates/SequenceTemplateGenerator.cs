using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace LoadKit.Core.Scenarios.Templates;

public sealed class SequenceTemplateGenerator : ITemplateGenerator
{
    public string Name => "seq";

    public string Usage => "{{seq}}";

    public bool IsNumeric => true;

    public bool TryBind(
        IReadOnlyList<string> arguments,
        [NotNullWhen(true)] out TemplateValueFactory? valueFactory,
        [NotNullWhen(false)] out string? error)
    {
        return TemplateArguments.TryBindWithoutArguments(
            Name,
            arguments,
            static context => context.NextSequence().ToString(CultureInfo.InvariantCulture),
            out valueFactory,
            out error);
    }
}
