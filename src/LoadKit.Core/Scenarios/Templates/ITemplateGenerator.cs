using System.Diagnostics.CodeAnalysis;

namespace LoadKit.Core.Scenarios.Templates;

/// <summary>
/// A <c>{{name:arg1:arg2}}</c> template. To add one: implement this interface, register it in
/// <see cref="TemplateRegistry.CreateDefault"/> and add a row to SCENARIO_REFERENCE.md.
/// Generated values are inserted into JSON strings without escaping, so they must not contain
/// quotes, backslashes or control characters.
/// </summary>
public interface ITemplateGenerator
{
    /// <summary>Name inside the braces, for example <c>randomInt</c>.</summary>
    string Name { get; }

    /// <summary>How the template is written in a scenario, for hints, for example <c>{{randomInt:MIN:MAX}}</c>.</summary>
    string Usage { get; }

    /// <summary>A body string that consists only of a numeric template is written as a JSON number.</summary>
    bool IsNumeric { get; }

    /// <summary>Parses arguments once, at compile time.</summary>
    bool TryBind(
        IReadOnlyList<string> arguments,
        [NotNullWhen(true)] out TemplateValueFactory? valueFactory,
        [NotNullWhen(false)] out string? error);
}
