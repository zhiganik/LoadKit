namespace LoadKit.Core.Auth;

/// <summary>
/// Base for providers with expiring tokens. The token is cached and applied from a field (hot path); a timer refreshes
/// it at ~80% of its lifetime; a 401 triggers an out-of-band refresh at most once every 5 seconds. Refreshes are
/// serialized, and a refresh started for a token that has already been replaced is skipped, so concurrent triggers
/// acquire the token once. A failed background refresh keeps the old token and retries after 5 seconds.
/// </summary>
public abstract class TokenAuthProviderBase : IAuthProvider
{
    public static readonly TimeSpan MinimumStaleRefreshInterval = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan RefreshRetryDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MinimumRefreshDelay = TimeSpan.FromSeconds(1);

    // Timers reject due times above ~49 days; a long-lived token is simply refreshed early.
    private static readonly TimeSpan MaximumRefreshDelay = TimeSpan.FromDays(1);
    private const double RefreshAtLifetimeFraction = 0.8;

    private readonly string _headerName;
    private readonly string _headerFormat;
    private readonly TimeProvider _timeProvider;
    private readonly SecretMasker _secretMasker;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private volatile CachedToken? _currentToken;
    private volatile string? _lastRefreshError;
    private ITimer? _refreshTimer;
    private long _lastStaleRefreshUtcTicks = long.MinValue;
    private int _acquireCount;
    private int _refreshFailureCount;
    private int _staleSignalCount;
    private volatile bool _disposed;

    protected TokenAuthProviderBase(string headerName, string headerFormat, TimeProvider timeProvider, SecretMasker secretMasker)
    {
        _headerName = headerName;
        _headerFormat = headerFormat;
        _timeProvider = timeProvider;
        _secretMasker = secretMasker;
    }

    /// <summary>Expiry of the current token; null when unknown (treated as non-expiring) or not acquired yet.</summary>
    public DateTimeOffset? ExpiresAt => _currentToken?.ExpiresAt;

    /// <summary>Successful token acquisitions, including the first one.</summary>
    public int AcquireCount => Volatile.Read(ref _acquireCount);

    public int RefreshFailureCount => Volatile.Read(ref _refreshFailureCount);

    /// <summary>Message of the last failed background refresh, masked.</summary>
    public string? LastRefreshError => _lastRefreshError;

    /// <summary>How many 401 responses were reported.</summary>
    public int StaleSignalCount => Volatile.Read(ref _staleSignalCount);

    protected TimeProvider TimeProvider => _timeProvider;

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        return RefreshAsync(CurrentGeneration, cancellationToken);
    }

    public ValueTask ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = _currentToken ?? throw new InvalidOperationException("InitializeAsync must complete before ApplyAsync.");
        request.Headers.Remove(_headerName);
        request.Headers.TryAddWithoutValidation(_headerName, token.HeaderValue);
        return ValueTask.CompletedTask;
    }

    public void MarkStale()
    {
        Interlocked.Increment(ref _staleSignalCount);
        var nowTicks = _timeProvider.GetUtcNow().UtcTicks;
        var lastTicks = Interlocked.Read(ref _lastStaleRefreshUtcTicks);
        var throttled = lastTicks != long.MinValue && nowTicks - lastTicks < MinimumStaleRefreshInterval.Ticks;
        if (throttled || Interlocked.CompareExchange(ref _lastStaleRefreshUtcTicks, nowTicks, lastTicks) != lastTicks)
        {
            return;
        }

        _ = RefreshInBackgroundAsync(CurrentGeneration);
    }

    public void Dispose()
    {
        _disposed = true;
        Interlocked.Exchange(ref _refreshTimer, null)?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Gets a new token from the source. Throw <see cref="AuthException"/> with a hint for failures the user can fix.
    /// </summary>
    protected abstract Task<AccessTokenResult> AcquireTokenAsync(CancellationToken cancellationToken);

    private int CurrentGeneration => _currentToken?.Generation ?? 0;

    /// <param name="observedGeneration">Generation the caller saw; if the token changed meanwhile, nothing is done.</param>
    private async Task RefreshAsync(int observedGeneration, CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (CurrentGeneration != observedGeneration || _disposed)
            {
                return;
            }

            var result = await AcquireTokenAsync(cancellationToken);
            if (string.IsNullOrEmpty(result.Token))
            {
                throw new AuthException("the token source returned an empty token", "check the token source response");
            }

            _secretMasker.AddSecret(result.Token);
            var generation = observedGeneration + 1;
            _currentToken = new CachedToken(AuthHeaderFormat.Apply(_headerFormat, result.Token), result.ExpiresAt, generation);
            Interlocked.Increment(ref _acquireCount);
            ScheduleRefresh(RefreshDelay(result.ExpiresAt), generation);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task RefreshInBackgroundAsync(int observedGeneration)
    {
        try
        {
            await RefreshAsync(observedGeneration, CancellationToken.None);
        }
        catch (Exception exception) when (!_disposed)
        {
            // The old token stays in use; requests that fail with 401 show up in the metrics.
            Interlocked.Increment(ref _refreshFailureCount);
            _lastRefreshError = _secretMasker.MaskText(exception.Message);
            ScheduleRefresh(RefreshRetryDelay, observedGeneration);
        }
        catch (Exception) when (_disposed)
        {
            // The run is over; nobody needs the token.
        }
    }

    private TimeSpan? RefreshDelay(DateTimeOffset? expiresAt)
    {
        if (expiresAt is not { } expiry)
        {
            return null;
        }

        var delay = (expiry - _timeProvider.GetUtcNow()) * RefreshAtLifetimeFraction;
        return delay < MinimumRefreshDelay ? MinimumRefreshDelay
            : delay > MaximumRefreshDelay ? MaximumRefreshDelay
            : delay;
    }

    private void ScheduleRefresh(TimeSpan? delay, int generation)
    {
        if (delay is not { } dueTime || _disposed)
        {
            return;
        }

        var timer = _timeProvider.CreateTimer(
            static state =>
            {
                var (provider, observedGeneration) = ((TokenAuthProviderBase, int))state!;
                _ = provider.RefreshInBackgroundAsync(observedGeneration);
            },
            (this, generation),
            dueTime,
            Timeout.InfiniteTimeSpan);
        Interlocked.Exchange(ref _refreshTimer, timer)?.Dispose();
    }

    private sealed record CachedToken(string HeaderValue, DateTimeOffset? ExpiresAt, int Generation);
}
