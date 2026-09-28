using System.Buffers.Text;
using System.Text.Json;

namespace LoadKit.Core.Auth;

/// <summary>Reads the <c>exp</c> claim of a JWT without validating it; LoadKit only needs to know when to refresh.</summary>
internal static class JwtExpiry
{
    public static DateTimeOffset? TryRead(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3 || !Base64Url.IsValid(parts[1]))
        {
            return null;
        }

        try
        {
            using var payload = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1]));
            if (payload.RootElement.ValueKind == JsonValueKind.Object
                && payload.RootElement.TryGetProperty("exp", out var expiry)
                && expiry.TryGetInt64(out var unixSeconds))
            {
                return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
            }
        }
        catch (JsonException)
        {
            // Not a JWT payload: lifetime unknown.
        }
        catch (ArgumentOutOfRangeException)
        {
            // exp outside the DateTimeOffset range: lifetime unknown.
        }

        return null;
    }
}
