using Microsoft.Extensions.Options;

namespace TargetApi;

/// <summary>
/// Decides whether a request to <c>/secure</c> carries an accepted credential:
/// the static dev token, an API key in <c>x-api-key</c>, or a token issued by this API.
/// </summary>
public sealed class CredentialChecker(TokenStore tokenStore, IOptions<TargetApiOptions> options)
{
    public const string ApiKeyHeaderName = "x-api-key";
    private const string BearerPrefix = "Bearer ";

    public bool IsAuthorized(HttpRequest request)
    {
        if (request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKey)
            && string.Equals(apiKey, options.Value.ApiKey, StringComparison.Ordinal))
        {
            return true;
        }

        var authorization = request.Headers.Authorization.ToString();
        if (!authorization.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var token = authorization[BearerPrefix.Length..].Trim();
        return string.Equals(token, options.Value.DevToken, StringComparison.Ordinal)
            || tokenStore.IsValid(token);
    }
}
