using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace LoadKit.Core.Scenarios.Templates;

/// <summary><c>{{now}}</c>: current UTC time in ISO 8601 with seconds, for example <c>2026-09-24T10:15:30Z</c>.</summary>
public sealed class NowTemplateGenerator : ITemplateGenerator
{
    private const string Iso8601UtcFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    public string Name => "now";

    public string Usage => "{{now}}";

    public bool IsNumeric => false;

    public bool TryBind(
        IReadOnlyList<string> arguments,
        [NotNullWhen(true)] out TemplateValueFactory? valueFactory,
        [NotNullWhen(false)] out string? error)
    {
        return TemplateArguments.TryBindWithoutArguments(
            Name,
            arguments,
            static context => context.TimeProvider.GetUtcNow().UtcDateTime.ToString(Iso8601UtcFormat, CultureInfo.InvariantCulture),
            out valueFactory,
            out error);
    }
}
