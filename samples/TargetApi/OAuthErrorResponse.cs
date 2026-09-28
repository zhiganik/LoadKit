using System.Text.Json.Serialization;

namespace TargetApi;

/// <summary>OAuth2 error response (RFC 6749, section 5.2).</summary>
public sealed record OAuthErrorResponse(
    [property: JsonPropertyName("error")] string Error);
