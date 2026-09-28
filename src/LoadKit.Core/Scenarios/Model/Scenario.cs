namespace LoadKit.Core.Scenarios.Model;

/// <summary>
/// A validated scenario with defaults applied and <c>${env:}</c> values substituted.
/// May contain secrets: never log it; <see cref="ToString"/> prints only non-secret fields.
/// </summary>
public sealed record Scenario(
    int Version,
    string Name,
    string? Description,
    string BaseUrl,
    IReadOnlyDictionary<string, string> Headers,
    AuthOptions? Auth,
    bool TagRuns,
    LoadOptions Load,
    IReadOnlyList<RequestDefinition> Requests,
    Thresholds? Thresholds)
{
    public override string ToString()
    {
        return $"Scenario {{ Name = {Name}, Requests = {Requests.Count}, Auth = {Auth?.Type ?? "none"} }}";
    }
}
