using Azure.Core;
using LoadKit.Core.Auth.Providers;
using LoadKit.Core.Scenarios.Model;

namespace LoadKit.Core.Auth;

/// <summary>Creates the provider for <c>auth.type</c>. The provider is not initialized; preflight does that.</summary>
/// <param name="tokenHttpClient">Client for login / token endpoints; must not go through <see cref="AuthHandler"/>.</param>
/// <param name="createCredential">Credential for <c>azureIdentity</c>; tests pass a fake.</param>
public sealed class AuthProviderFactory(
    HttpClient tokenHttpClient,
    TimeProvider timeProvider,
    SecretMasker secretMasker,
    Func<AzureIdentityAuth, TokenCredential> createCredential)
{
    public AuthProviderFactory(HttpClient tokenHttpClient, TimeProvider timeProvider, SecretMasker secretMasker)
        : this(tokenHttpClient, timeProvider, secretMasker, AzureIdentityAuthProvider.CreateCredential)
    {
    }

    /// <returns>Null when the scenario has no <c>auth</c>.</returns>
    public IAuthProvider? Create(Scenario scenario)
    {
        return scenario.Auth switch
        {
            null => null,
            BearerAuth bearer => new BearerAuthProvider(bearer),
            ApiKeyAuth apiKey => new ApiKeyAuthProvider(apiKey),
            LoginAuth login => new LoginAuthProvider(login, scenario.BaseUrl, tokenHttpClient, timeProvider, secretMasker),
            OAuth2ClientCredentialsAuth oauth2 => new OAuth2ClientCredentialsAuthProvider(oauth2, tokenHttpClient, timeProvider, secretMasker),
            AzureIdentityAuth azureIdentity => new AzureIdentityAuthProvider(azureIdentity, createCredential(azureIdentity), timeProvider, secretMasker),
            _ => throw new InvalidOperationException($"No provider for auth type '{scenario.Auth.Type}'."),
        };
    }
}
