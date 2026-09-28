using Azure.Core;
using Azure.Identity;
using LoadKit.Core.Scenarios.Model;

namespace LoadKit.Core.Auth.Providers;

/// <summary>
/// <c>azureIdentity</c>: a token for <c>scope</c> from <see cref="AzureCliCredential"/> (<c>source: azureCli</c>, default)
/// or <see cref="DefaultAzureCredential"/> (<c>source: default</c>). No secrets in files.
/// </summary>
public sealed class AzureIdentityAuthProvider : TokenAuthProviderBase
{
    /// <summary>Client id of the "Microsoft Azure CLI" application, which the API must authorize (AADSTS65001).</summary>
    public const string AzureCliClientId = "04b07795-8ddb-461a-bbee-02f9e1bf7b46";

    private readonly AzureIdentityAuth _options;
    private readonly TokenCredential _credential;

    public AzureIdentityAuthProvider(AzureIdentityAuth options, TokenCredential credential, TimeProvider timeProvider, SecretMasker secretMasker)
        : base(ScenarioDefaults.AuthHeader, ScenarioDefaults.AuthFormat, timeProvider, secretMasker)
    {
        _options = options;
        _credential = credential;
    }

    /// <summary>The credential for <c>auth.source</c>.</summary>
    public static TokenCredential CreateCredential(AzureIdentityAuth options)
    {
        return options.Source == AzureIdentitySources.Default ? new DefaultAzureCredential() : new AzureCliCredential();
    }

    protected override async Task<AccessTokenResult> AcquireTokenAsync(CancellationToken cancellationToken)
    {
        try
        {
            var accessToken = await _credential.GetTokenAsync(new TokenRequestContext([_options.Scope]), cancellationToken);
            return new AccessTokenResult(accessToken.Token, accessToken.ExpiresOn);
        }
        catch (CredentialUnavailableException exception)
        {
            var hint = _options.Source == AzureIdentitySources.Default
                ? "sign in with 'az login' or Visual Studio, or set the AZURE_* environment variables"
                : "run 'az login' (and 'az account set --subscription <id>' for the right tenant); Azure CLI must be installed";
            throw new AuthException($"azureIdentity: no credential available: {FirstLine(exception.Message)}", hint, exception);
        }
        catch (AuthenticationFailedException exception) when (exception.Message.Contains("AADSTS65001", StringComparison.Ordinal))
        {
            throw new AuthException(
                $"azureIdentity: the API does not allow Azure CLI to get tokens for {_options.Scope} (AADSTS65001)",
                $"the owner of the API's app registration adds client id {AzureCliClientId} under Expose an API → Authorized client applications",
                exception);
        }
        catch (AuthenticationFailedException exception)
        {
            throw new AuthException(
                $"azureIdentity: token for {_options.Scope} failed: {FirstLine(exception.Message)}",
                "check auth.scope (api://<application-id-uri>/.default) and that your account has access to the API",
                exception);
        }
    }

    private static string FirstLine(string message)
    {
        var lineEnd = message.IndexOfAny(['\r', '\n']);
        return lineEnd < 0 ? message : message[..lineEnd];
    }
}
