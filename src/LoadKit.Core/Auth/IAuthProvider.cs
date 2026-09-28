namespace LoadKit.Core.Auth;

/// <summary>Acquires credentials before the run and applies them to every request. See docs/architecture/AUTH.md.</summary>
public interface IAuthProvider
{
    /// <summary>Preflight: acquire the first token. A failure means exit code 3.</summary>
    Task InitializeAsync(CancellationToken cancellationToken);

    /// <summary>Hot path: reads cached credentials only, never calls the network.</summary>
    ValueTask ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken);

    /// <summary>Called after a 401 response. Static credentials only count it; the request is never retried.</summary>
    void MarkStale();
}
