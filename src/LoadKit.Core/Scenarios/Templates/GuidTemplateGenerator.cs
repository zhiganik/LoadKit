using System.Diagnostics.CodeAnalysis;

namespace LoadKit.Core.Scenarios.Templates;

public sealed class GuidTemplateGenerator : ITemplateGenerator
{
    public string Name => "guid";

    public string Usage => "{{guid}}";

    public bool IsNumeric => false;

    public bool TryBind(
        IReadOnlyList<string> arguments,
        [NotNullWhen(true)] out TemplateValueFactory? valueFactory,
        [NotNullWhen(false)] out string? error)
    {
        return TemplateArguments.TryBindWithoutArguments(Name, arguments, static _ => Guid.NewGuid().ToString(), out valueFactory, out error);
    }
}
