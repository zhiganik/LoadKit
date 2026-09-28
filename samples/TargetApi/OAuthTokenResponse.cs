using System.Text.Json.Serialization;

namespace TargetApi;

/// <summary>OAuth2 token endpoint response (RFC 6749, section 5.1).</summary>
public sealed record OAuthTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);
