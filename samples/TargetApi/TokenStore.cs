using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace TargetApi;

/// <summary>
/// Issues opaque short-lived tokens and checks them. Tokens live only in memory.
/// </summary>
public sealed class TokenStore(TimeProvider timeProvider, IOptions<TargetApiOptions> options)
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _expiresAtByToken = new(StringComparer.Ordinal);

    public IssuedToken Issue()
    {
        var lifetimeSeconds = options.Value.TokenLifetimeSeconds;
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expiresAt = timeProvider.GetUtcNow().AddSeconds(lifetimeSeconds);
        _expiresAtByToken[token] = expiresAt;
        RemoveExpired();
        return new IssuedToken(token, lifetimeSeconds);
    }

    public bool IsValid(string token)
    {
        return _expiresAtByToken.TryGetValue(token, out var expiresAt)
            && expiresAt > timeProvider.GetUtcNow();
    }

    private void RemoveExpired()
    {
        var now = timeProvider.GetUtcNow();
        foreach (var (token, expiresAt) in _expiresAtByToken)
        {
            if (expiresAt <= now)
            {
                _expiresAtByToken.TryRemove(token, out _);
            }
        }
    }
}
