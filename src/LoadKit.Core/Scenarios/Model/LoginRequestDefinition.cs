using System.Text.Json;

namespace LoadKit.Core.Scenarios.Model;

/// <summary>The <c>auth.request</c> of a <c>login</c> auth; path is relative to <c>baseUrl</c>. Body usually holds a password.</summary>
public sealed record LoginRequestDefinition(
    string Method,
    string Path,
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyDictionary<string, string> Query,
    JsonElement? Body,
    string? BodyRaw,
    string ContentType)
{
    public override string ToString()
    {
        return $"LoginRequestDefinition {{ Method = {Method}, Path = {Path} }}";
    }
}
