using System.Net;
using LoadKit.Core.Auth;
using LoadKit.Core.Engine;
using LoadKit.Core.Scenarios;
using LoadKit.Core.Tests.TestSupport;
using Microsoft.Extensions.Time.Testing;

namespace LoadKit.Core.Tests.Engine;

public sealed class PreflightCheckerTests
{
    private readonly FakeTimeProvider _timeProvider = new();

    [Fact]
    public async Task UnreachableBaseUrl_Fails_WithoutTouchingAuth()
    {
        var handler = new FakeHttpMessageHandler((_, _) => throw new HttpRequestException(HttpRequestError.ConnectionError, "refused"));
        var authProvider = new RecordingAuthProvider();

        var result = await RunAsync(handler, authProvider);

        var step = Assert.Single(result.Steps);
        Assert.False(result.Succeeded);
        Assert.Equal(PreflightChecker.BaseUrlStep, step.Name);
        Assert.Contains("is not reachable", step.Message, StringComparison.Ordinal);
        Assert.Contains("API is running", step.Hint, StringComparison.Ordinal);
        Assert.False(authProvider.Initialized);
    }

    [Fact]
    public async Task AnyHttpResponse_MeansReachable()
    {
        var result = await RunAsync(FakeHttpMessageHandler.Returning(HttpStatusCode.NotFound), authProvider: null);

        var step = Assert.Single(result.Steps);
        Assert.True(result.Succeeded);
        Assert.Equal("http://localhost:5080 is reachable (HTTP 404)", step.Message);
    }

    [Fact]
    public async Task AuthFailure_FailsWithProviderHint()
    {
        var authProvider = new RecordingAuthProvider(new AuthException("login returned 401", "check .env"));

        var result = await RunAsync(FakeHttpMessageHandler.Returning(HttpStatusCode.OK), authProvider);

        Assert.False(result.Succeeded);
        var authStep = result.Steps[1];
        Assert.Equal((PreflightChecker.AuthStep, false, "bearer: login returned 401", "check .env"), (authStep.Name, authStep.Succeeded, authStep.Message, authStep.Hint));
    }

    [Fact]
    public async Task TokenWithoutLifetime_SucceedsWithWarning()
    {
        using var authProvider = new FixedTokenProvider(_timeProvider, expiresAt: null);

        var result = await RunAsync(FakeHttpMessageHandler.Returning(HttpStatusCode.OK), authProvider);

        Assert.True(result.Succeeded);
        Assert.Contains("non-expiring", result.Steps[1].Warning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TokenWithLifetime_ReportsIt()
    {
        using var authProvider = new FixedTokenProvider(_timeProvider, _timeProvider.GetUtcNow().AddMinutes(60));

        var result = await RunAsync(FakeHttpMessageHandler.Returning(HttpStatusCode.OK), authProvider);

        Assert.Equal("bearer: token acquired, expires in 60 min", result.Steps[1].Message);
        Assert.Null(result.Steps[1].Warning);
    }

    private async Task<PreflightResult> RunAsync(HttpMessageHandler handler, IAuthProvider? authProvider)
    {
        // The scenario type only names the step; the fake provider does the work.
        var scenario = TestScenarios.Load("""
            {
              "version": 1, "name": "t", "baseUrl": "http://localhost:5080",
              "auth": { "type": "bearer", "token": "${env:TOKEN}" },
              "load": { "concurrency": 1, "totalRequests": 1 },
              "requests": [ { "name": "r", "method": "GET", "path": "/", "expect": { "status": [200] } } ]
            }
            """, new Dictionary<string, string> { ["TOKEN"] = "token-value" }).Scenario!;
        using var httpClient = new HttpClient(handler);
        var checker = new PreflightChecker(httpClient, _timeProvider, SecretMasker.CreateEmpty());
        return await checker.RunAsync(scenario, authProvider, TestContext.Current.CancellationToken);
    }

    private sealed class RecordingAuthProvider(Exception? failure = null) : IAuthProvider
    {
        public bool Initialized { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            Initialized = true;
            return failure is null ? Task.CompletedTask : Task.FromException(failure);
        }

        public ValueTask ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }

        public void MarkStale()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class FixedTokenProvider(TimeProvider timeProvider, DateTimeOffset? expiresAt)
        : TokenAuthProviderBase("Authorization", "Bearer {token}", timeProvider, SecretMasker.CreateEmpty())
    {
        protected override Task<AccessTokenResult> AcquireTokenAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new AccessTokenResult("fixed-token", expiresAt));
        }
    }
}
