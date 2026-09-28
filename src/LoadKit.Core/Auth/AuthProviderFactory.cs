using LoadKit.Core.Auth.Providers;
using LoadKit.Core.Scenarios.Model;

namespace LoadKit.Core.Auth;

/// <summary>Creates the provider for <c>auth.type</c>; null when the scenario has no <c>auth</c>.</summary>
public static class AuthProviderFactory
{
    /// <exception cref="NotSupportedException">The auth type is not implemented yet (see docs/PLAN.md, phase 3).</exception>
    public static IAuthProvider? Create(AuthOptions? authOptions)
    {
        return authOptions switch
        {
            null => null,
            BearerAuth bearer => new BearerAuthProvider(bearer),
            ApiKeyAuth apiKey => new ApiKeyAuthProvider(apiKey),
            _ => throw new NotSupportedException($"auth type '{authOptions.Type}' is not supported by run yet"),
        };
    }
}
