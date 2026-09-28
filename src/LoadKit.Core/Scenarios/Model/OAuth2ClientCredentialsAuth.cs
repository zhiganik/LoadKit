namespace LoadKit.Core.Scenarios.Model;

public sealed record OAuth2ClientCredentialsAuth(string TokenUrl, string ClientId, string ClientSecret, string Scope) : AuthOptions
{
    public override string Type => AuthTypeNames.OAuth2ClientCredentials;

    public override string ToString()
    {
        return $"OAuth2ClientCredentialsAuth {{ TokenUrl = {TokenUrl}, Scope = {Scope} }}";
    }
}
