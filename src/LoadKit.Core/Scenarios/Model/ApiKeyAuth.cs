namespace LoadKit.Core.Scenarios.Model;

/// <summary>Static key sent in a header or a query parameter; exactly one of them is set.</summary>
public sealed record ApiKeyAuth(string Value, string? Header, string? Query) : AuthOptions
{
    public override string Type => AuthTypeNames.ApiKey;

    public override string ToString()
    {
        return $"ApiKeyAuth {{ Header = {Header}, Query = {Query} }}";
    }
}
