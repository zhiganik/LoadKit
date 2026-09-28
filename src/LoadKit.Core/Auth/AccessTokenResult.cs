namespace LoadKit.Core.Auth;

/// <param name="ExpiresAt">Null when the lifetime is unknown; the token is then treated as non-expiring.</param>
public readonly record struct AccessTokenResult(string Token, DateTimeOffset? ExpiresAt);
