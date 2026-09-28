namespace LoadKit.Core.Auth;

/// <summary>
/// Acquires credentials before the run and applies them to every request. See docs/architecture/AUTH.md.
/// Dispose stops background token refresh.
/// </summary>
public interface IAuthProvider : IDisposable
{
    /// <summary>Preflight: acquire the first token. Throws <see cref="AuthException"/> with a hint on failure (exit code 3).</summary>
    Task InitializeAsync(CancellationToken cancellationToken);

    /// <summary>Hot path: reads cached credentials only, never calls the network.</summary>
    ValueTask ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken);

    /// <summary>Called after a 401 response. Static credentials only count it; the request is never retried.</summary>
    void MarkStale();
}
