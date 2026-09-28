using LoadKit.Core.Scenarios.Model;

namespace LoadKit.Core.Auth.Providers;

/// <summary><c>bearer</c>: a ready token from <c>${env:}</c>, sent in a header using <c>format</c>. It cannot be refreshed.</summary>
public sealed class BearerAuthProvider : IAuthProvider
{
    private readonly string _headerName;
    private readonly string _headerValue;
    private int _staleSignalCount;

    public BearerAuthProvider(BearerAuth options)
    {
        _headerName = options.Header;
        _headerValue = AuthHeaderFormat.Apply(options.Format, options.Token);
    }

    /// <summary>How many 401 responses were received; the token may have expired.</summary>
    public int StaleSignalCount => Volatile.Read(ref _staleSignalCount);

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public ValueTask ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Remove(_headerName);
        request.Headers.TryAddWithoutValidation(_headerName, _headerValue);
        return ValueTask.CompletedTask;
    }

    public void MarkStale()
    {
        Interlocked.Increment(ref _staleSignalCount);
    }
}
