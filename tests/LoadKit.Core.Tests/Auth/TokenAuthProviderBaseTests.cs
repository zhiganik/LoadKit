using LoadKit.Core.Auth;
using Microsoft.Extensions.Time.Testing;

namespace LoadKit.Core.Tests.Auth;

public sealed class TokenAuthProviderBaseTests
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(100);

    private readonly FakeTimeProvider _timeProvider = new();
    private readonly SecretMasker _secretMasker = SecretMasker.CreateEmpty();

    [Fact]
    public async Task Initialize_AcquiresToken_ApplyIsSynchronous_AndTokenIsMasked()
    {
        using var provider = CreateProvider(callNumber => Token(callNumber, Lifetime));

        await provider.InitializeAsync(TestContext.Current.CancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/");
        var apply = provider.ApplyAsync(request, TestContext.Current.CancellationToken);

        Assert.True(apply.IsCompletedSuccessfully);
        Assert.Equal("Bearer token-1", Header(request));
        Assert.Equal(_timeProvider.GetUtcNow() + Lifetime, provider.ExpiresAt);
        Assert.Equal("sent *** here", _secretMasker.MaskText("sent token-1 here"));
    }

    [Fact]
    public void Apply_BeforeInitialize_Throws()
    {
        using var provider = CreateProvider(callNumber => Token(callNumber, Lifetime));
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/");

        Assert.Throws<InvalidOperationException>(() => provider.ApplyAsync(request, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Initialize_PropagatesAuthException()
    {
        using var provider = CreateProvider(_ => throw new AuthException("bad credentials", "check .env"));

        var exception = await Assert.ThrowsAsync<AuthException>(() => provider.InitializeAsync(TestContext.Current.CancellationToken));

        Assert.Equal("check .env", exception.Hint);
    }

    [Fact]
    public async Task Token_IsRefreshedAt80PercentOfLifetime()
    {
        using var provider = CreateProvider(callNumber => Token(callNumber, Lifetime));
        await provider.InitializeAsync(TestContext.Current.CancellationToken);

        _timeProvider.Advance(TimeSpan.FromSeconds(79));
        Assert.Equal(1, provider.CallCount);

        _timeProvider.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(2, provider.CallCount);
        Assert.Equal("Bearer token-2", await AppliedHeaderAsync(provider));

        _timeProvider.Advance(TimeSpan.FromSeconds(80));
        Assert.Equal(3, provider.CallCount);
    }

    [Fact]
    public async Task VeryLongLifetime_IsRefreshedAfterADay()
    {
        using var provider = CreateProvider(callNumber => Token(callNumber, TimeSpan.FromDays(3650)));
        await provider.InitializeAsync(TestContext.Current.CancellationToken);

        _timeProvider.Advance(TimeSpan.FromDays(1));

        Assert.Equal(2, provider.CallCount);
    }

    [Fact]
    public async Task UnknownLifetime_IsNeverRefreshedByTimer()
    {
        using var provider = CreateProvider(callNumber => Task.FromResult(new AccessTokenResult($"token-{callNumber}", null)));
        await provider.InitializeAsync(TestContext.Current.CancellationToken);

        _timeProvider.Advance(TimeSpan.FromDays(1));

        Assert.Equal(1, provider.CallCount);
        Assert.Null(provider.ExpiresAt);
    }

    [Fact]
    public async Task ConcurrentStaleSignals_RefreshExactlyOnce()
    {
        var gate = new TaskCompletionSource();
        using var provider = CreateProvider(async callNumber =>
        {
            if (callNumber > 1)
            {
                await gate.Task;
            }

            return new AccessTokenResult($"token-{callNumber}", _timeProvider.GetUtcNow() + Lifetime);
        });
        await provider.InitializeAsync(TestContext.Current.CancellationToken);

        Parallel.For(0, 50, _ => provider.MarkStale());
        gate.SetResult();
        await WaitUntilAsync(() => provider.AcquireCount == 2);

        Assert.Equal(2, provider.CallCount);
        Assert.Equal(2, provider.AcquireCount);
        Assert.Equal(50, provider.StaleSignalCount);
        Assert.Equal("Bearer token-2", await AppliedHeaderAsync(provider));
    }

    [Fact]
    public async Task StaleSignals_TriggerAtMostOneRefreshPer5Seconds()
    {
        using var provider = CreateProvider(callNumber => Token(callNumber, Lifetime));
        await provider.InitializeAsync(TestContext.Current.CancellationToken);

        provider.MarkStale();
        provider.MarkStale();
        Assert.Equal(2, provider.CallCount);

        _timeProvider.Advance(TokenAuthProviderBase.MinimumStaleRefreshInterval);
        provider.MarkStale();
        Assert.Equal(3, provider.CallCount);
    }

    [Fact]
    public async Task FailedBackgroundRefresh_KeepsOldToken_AndRetries()
    {
        using var provider = CreateProvider(callNumber => callNumber == 2
            ? throw new AuthException("token endpoint down", null)
            : Token(callNumber, Lifetime));
        await provider.InitializeAsync(TestContext.Current.CancellationToken);

        _timeProvider.Advance(TimeSpan.FromSeconds(81));
        Assert.Equal(1, provider.RefreshFailureCount);
        Assert.Equal("token endpoint down", provider.LastRefreshError);
        Assert.Equal("Bearer token-1", await AppliedHeaderAsync(provider));

        _timeProvider.Advance(TokenAuthProviderBase.RefreshRetryDelay);
        Assert.Equal(3, provider.CallCount);
        Assert.Equal("Bearer token-3", await AppliedHeaderAsync(provider));
    }

    [Fact]
    public async Task Dispose_StopsRefresh()
    {
        var provider = CreateProvider(callNumber => Token(callNumber, Lifetime));
        await provider.InitializeAsync(TestContext.Current.CancellationToken);

        provider.Dispose();
        _timeProvider.Advance(TimeSpan.FromHours(1));

        Assert.Equal(1, provider.CallCount);
    }

    private ScriptedTokenProvider CreateProvider(Func<int, Task<AccessTokenResult>> acquire)
    {
        return new ScriptedTokenProvider(_timeProvider, _secretMasker, acquire);
    }

    private Task<AccessTokenResult> Token(int callNumber, TimeSpan lifetime)
    {
        return Task.FromResult(new AccessTokenResult($"token-{callNumber}", _timeProvider.GetUtcNow() + lifetime));
    }

    private static async Task<string> AppliedHeaderAsync(IAuthProvider provider)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/");
        await provider.ApplyAsync(request, TestContext.Current.CancellationToken);
        return Header(request);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(condition());
    }

    private static string Header(HttpRequestMessage request)
    {
        return Assert.Single(request.Headers.GetValues("Authorization"));
    }

    private sealed class ScriptedTokenProvider(TimeProvider timeProvider, SecretMasker secretMasker, Func<int, Task<AccessTokenResult>> acquire)
        : TokenAuthProviderBase("Authorization", "Bearer {token}", timeProvider, secretMasker)
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        protected override Task<AccessTokenResult> AcquireTokenAsync(CancellationToken cancellationToken)
        {
            return acquire(Interlocked.Increment(ref _callCount));
        }
    }
}
