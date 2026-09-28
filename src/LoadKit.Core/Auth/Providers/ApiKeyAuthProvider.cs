using LoadKit.Core.Scenarios.Model;

namespace LoadKit.Core.Auth.Providers;

/// <summary>
/// <c>apiKey</c>: a static key in a header or in a query parameter. It cannot be refreshed.
/// </summary>
public sealed class ApiKeyAuthProvider : IAuthProvider
{
    private readonly string? _headerName;
    private readonly string _value;
    private readonly string? _escapedQueryParameter;
    private int _staleSignalCount;

    public ApiKeyAuthProvider(ApiKeyAuth options)
    {
        _value = options.Value;
        _headerName = options.Header;
        if (options.Header is null)
        {
            var queryName = options.Query ?? throw new ArgumentException("apiKey needs a header or a query parameter.", nameof(options));
            _escapedQueryParameter = $"{Uri.EscapeDataString(queryName)}={Uri.EscapeDataString(options.Value)}";
        }
    }

    /// <summary>How many 401 responses were received; the key may be wrong or revoked.</summary>
    public int StaleSignalCount => Volatile.Read(ref _staleSignalCount);

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public ValueTask ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_headerName is not null)
        {
            request.Headers.Remove(_headerName);
            request.Headers.TryAddWithoutValidation(_headerName, _value);
        }
        else if (request.RequestUri is { } requestUri)
        {
            var url = requestUri.OriginalString;
            var separator = url.Contains('?', StringComparison.Ordinal) ? "&" : "?";
            request.RequestUri = new Uri(string.Concat(url, separator, _escapedQueryParameter), UriKind.RelativeOrAbsolute);
        }

        return ValueTask.CompletedTask;
    }

    public void MarkStale()
    {
        Interlocked.Increment(ref _staleSignalCount);
    }

    public void Dispose()
    {
        // Nothing to release: the key is static.
    }
}
