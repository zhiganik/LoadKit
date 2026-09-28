namespace LoadKit.Core.Auth;

/// <summary>Builds a header value from the scenario <c>format</c>, e.g. <c>Bearer {token}</c>.</summary>
internal static class AuthHeaderFormat
{
    public const string TokenPlaceholder = "{token}";

    public static string Apply(string format, string token)
    {
        return format.Replace(TokenPlaceholder, token, StringComparison.Ordinal);
    }
}
