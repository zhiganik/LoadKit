namespace LoadKit.Core.Scenarios.Model;

/// <summary>Token from the API's own login endpoint, read by JSONPath from the response.</summary>
public sealed record LoginAuth(
    LoginRequestDefinition Request,
    string TokenPath,
    string? ExpiresInPath,
    string Header,
    string Format) : AuthOptions
{
    public override string Type => AuthTypeNames.Login;

    public override string ToString()
    {
        return $"LoginAuth {{ Path = {Request.Path}, TokenPath = {TokenPath} }}";
    }
}
