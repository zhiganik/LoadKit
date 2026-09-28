using System.Text.Json;

namespace LoadKit.Core.Scenarios.Model;

/// <summary>
/// One entry of <c>requests[]</c>. <see cref="Method"/> is upper case. Strings may still contain <c>{{...}}</c> templates.
/// </summary>
public sealed record RequestDefinition(
    string Name,
    string Method,
    string Path,
    int Weight,
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyDictionary<string, string> Query,
    JsonElement? Body,
    string? BodyRaw,
    string ContentType,
    bool AllowEmptyBody,
    bool UseAuth,
    Expectation Expect)
{
    public override string ToString()
    {
        return $"RequestDefinition {{ Name = {Name}, Method = {Method}, Path = {Path} }}";
    }
}
