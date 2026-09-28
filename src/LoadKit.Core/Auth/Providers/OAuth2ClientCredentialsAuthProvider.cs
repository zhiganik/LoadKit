using System.Text.Json;
using LoadKit.Core.Scenarios.Model;

namespace LoadKit.Core.Auth.Providers;

/// <summary><c>oauth2ClientCredentials</c>: POSTs the client credentials grant to <c>tokenUrl</c>; lifetime from <c>expires_in</c>.</summary>
public sealed class OAuth2ClientCredentialsAuthProvider : TokenAuthProviderBase
{
    private readonly OAuth2ClientCredentialsAuth _options;
    private readonly Uri _tokenUri;
    private readonly HttpClient _tokenHttpClient;

    /// <param name="tokenHttpClient">A client without <see cref="AuthHandler"/>.</param>
    public OAuth2ClientCredentialsAuthProvider(
        OAuth2ClientCredentialsAuth options,
        HttpClient tokenHttpClient,
        TimeProvider timeProvider,
        SecretMasker secretMasker)
        : base(ScenarioDefaults.AuthHeader, ScenarioDefaults.AuthFormat, timeProvider, secretMasker)
    {
        _options = options;
        _tokenUri = new Uri(options.TokenUrl, UriKind.Absolute);
        _tokenHttpClient = tokenHttpClient;
    }

    protected override async Task<AccessTokenResult> AcquireTokenAsync(CancellationToken cancellationToken)
    {
        var requestDescription = $"token request to {_tokenUri.GetLeftPart(UriPartial.Path)}";
        using var content = new FormUrlEncodedContent(
        [
            new("grant_type", "client_credentials"),
            new("client_id", _options.ClientId),
            new("client_secret", _options.ClientSecret),
            new("scope", _options.Scope),
        ]);

        HttpResponseMessage response;
        try
        {
            response = await _tokenHttpClient.PostAsync(_tokenUri, content, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new AuthException($"{requestDescription} failed: {exception.Message}", "check auth.tokenUrl and the network", exception);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw CreateErrorException(requestDescription, (int)response.StatusCode, body);
            }

            using var document = LoginAuthProvider.ParseJson(body, requestDescription);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("access_token", out var accessToken)
                || accessToken.ValueKind != JsonValueKind.String)
            {
                throw new AuthException($"{requestDescription}: the response has no access_token", "check that auth.tokenUrl is an OAuth2 token endpoint");
            }

            DateTimeOffset? expiresAt = root.TryGetProperty("expires_in", out var expiresIn) && LoginAuthProvider.TryGetSeconds(expiresIn, out var seconds)
                ? TimeProvider.GetUtcNow().AddSeconds(seconds)
                : null;
            var token = accessToken.GetString()!;
            return new AccessTokenResult(token, expiresAt ?? JwtExpiry.TryRead(token));
        }
    }

    // RFC 6749 §5.2 error responses carry "error" and "error_description"; neither holds secrets.
    private static AuthException CreateErrorException(string requestDescription, int statusCode, string body)
    {
        string? error = null;
        string? description = null;
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                error = document.RootElement.TryGetProperty("error", out var errorElement) ? errorElement.ToString() : null;
                description = document.RootElement.TryGetProperty("error_description", out var descriptionElement) ? descriptionElement.ToString() : null;
            }
        }
        catch (JsonException)
        {
            // Not an OAuth2 error body; the status code is reported alone.
        }

        var hint = error switch
        {
            "invalid_client" or "unauthorized_client" => "check auth.clientId and auth.clientSecret in .env",
            "invalid_scope" => "check auth.scope; for Entra ID use api://<application-id-uri>/.default",
            "unsupported_grant_type" => "the token endpoint does not allow the client credentials grant for this client",
            _ => "check auth.tokenUrl, clientId, clientSecret and scope",
        };
        var details = error is null ? LoginAuthProvider.Truncate(body) : $"{error}{(description is null ? string.Empty : $": {LoginAuthProvider.Truncate(description)}")}";
        return new AuthException($"{requestDescription} returned {statusCode}: {details}", hint);
    }
}
