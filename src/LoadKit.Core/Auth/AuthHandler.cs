using System.Net;

namespace LoadKit.Core.Auth;

/// <summary>
/// Applies credentials to every request except those marked with <see cref="AuthRequestOptions.SkipAuth"/>,
/// and reports 401 responses to the provider.
/// </summary>
public sealed class AuthHandler(IAuthProvider authProvider) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Options.TryGetValue(AuthRequestOptions.SkipAuth, out var skipAuth) && skipAuth)
        {
            return base.SendAsync(request, cancellationToken);
        }

        return SendWithAuthAsync(request, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendWithAuthAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await authProvider.ApplyAsync(request, cancellationToken);
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            authProvider.MarkStale();
        }

        return response;
    }
}
