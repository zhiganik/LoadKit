using System.Collections.Concurrent;
using System.Net;
using LoadKit.Core.Auth;
using LoadKit.Core.Engine;
using LoadKit.Core.Metrics;
using LoadKit.Core.Scenarios;
using LoadKit.Core.Tests.TestSupport;

namespace LoadKit.Core.Tests.Engine;

public sealed class LoadRunnerTests
{
    [Fact]
    public async Task TotalRequests_AreAllSent_AndWarmupIsNotMeasured()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK);
        var scenario = Scenario(load: """{ "concurrency": 4, "totalRequests": 50, "warmup": 10 }""");

        var result = await RunAsync(handler, scenario, TestContext.Current.CancellationToken);

        Assert.Equal(50, handler.RequestCount);
        Assert.Equal(40, result.Statistics.Overall.Count);
        Assert.Equal(40, result.Results.Count);
        Assert.Equal(0, result.Statistics.Overall.ErrorCount);
        Assert.False(result.Interrupted);
        Assert.True(result.ThresholdsPassed);
    }

    [Fact]
    public async Task StatusIsCountedAsErrorOnlyWhenNotInExpect()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.InternalServerError, "boom");
        var scenario = Scenario(
            load: """{ "concurrency": 3, "totalRequests": 60 }""",
            requests: """
                { "name": "expected-500", "method": "GET", "path": "/a", "expect": { "status": [200, 500] } },
                { "name": "unexpected-500", "method": "GET", "path": "/b", "expect": { "status": [200] } }
                """);

        var result = await RunAsync(handler, scenario, TestContext.Current.CancellationToken);

        var expected = result.Statistics.Requests[0];
        var unexpected = result.Statistics.Requests[1];
        Assert.Equal(60, expected.Count + unexpected.Count);
        Assert.Equal(0, expected.ErrorCount);
        Assert.Equal(unexpected.Count, unexpected.ErrorCount);
        Assert.Equal([new ErrorKindCount(ErrorKind.UnexpectedStatus, unexpected.Count)], result.Statistics.Overall.Errors);
        Assert.InRange(result.ErrorSamples.Count, 1, 5);
        Assert.All(result.ErrorSamples, sample => Assert.Equal(("unexpected-500", 500, "boom"), (sample.RequestName, sample.StatusCode, sample.Text)));
    }

    [Fact]
    public async Task ErrorSamples_AreMasked()
    {
        const string Secret = "super-secret-token-value";
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.Unauthorized, $"token {Secret} rejected");
        var scenario = Scenario(load: """{ "concurrency": 1, "totalRequests": 3 }""");

        var result = await RunAsync(handler, scenario, TestContext.Current.CancellationToken, new SecretMasker([Secret], []));

        var sample = Assert.Single(result.ErrorSamples.DistinctBy(sample => sample.Text));
        Assert.Equal("token *** rejected", sample.Text);
    }

    [Fact]
    public async Task NoResponseWithinTimeout_IsTimeout()
    {
        var handler = new FakeHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("unreachable");
        });
        var scenario = Scenario(load: """{ "concurrency": 2, "totalRequests": 4, "timeoutMs": 50 }""");

        var result = await RunAsync(handler, scenario, TestContext.Current.CancellationToken);

        Assert.All(result.Results, requestResult => Assert.Equal((0, ErrorKind.Timeout), (requestResult.StatusCode, requestResult.Error)));
        Assert.Equal(4, result.Statistics.Overall.ErrorCount);
        Assert.Null(result.Statistics.Overall.Latency);
        Assert.Contains("load.timeoutMs", Assert.Single(result.ErrorSamples.DistinctBy(sample => sample.Text)).Text, StringComparison.Ordinal);
        Assert.False(result.Interrupted);
    }

    [Fact]
    public async Task ExpectedStatusSlowerThanMaxMs_IsSlowResponse()
    {
        var handler = new FakeHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(20, cancellationToken);
            return FakeHttpMessageHandler.Response(HttpStatusCode.OK);
        });
        var scenario = Scenario(
            load: """{ "concurrency": 2, "totalRequests": 4 }""",
            requests: """{ "name": "slow", "method": "GET", "path": "/slow", "expect": { "status": [200], "maxMs": 1 } }""");

        var result = await RunAsync(handler, scenario, TestContext.Current.CancellationToken);

        Assert.All(result.Results, requestResult => Assert.Equal((200, ErrorKind.SlowResponse), (requestResult.StatusCode, requestResult.Error)));
        Assert.Empty(result.ErrorSamples);
    }

    [Fact]
    public async Task ConnectionFailure_IsClassified()
    {
        var handler = new FakeHttpMessageHandler((_, _) =>
            throw new HttpRequestException(HttpRequestError.ConnectionError, "connection refused"));
        var scenario = Scenario(load: """{ "concurrency": 1, "totalRequests": 2 }""");

        var result = await RunAsync(handler, scenario, TestContext.Current.CancellationToken);

        Assert.Equal([new ErrorKindCount(ErrorKind.Connection, 2)], result.Statistics.Overall.Errors);
        Assert.Contains("connection refused", result.ErrorSamples[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancellation_StopsTheRun_AndKeepsCollectedResults()
    {
        using var interruption = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var sentCount = 0;
        var handler = new FakeHttpMessageHandler(async (_, cancellationToken) =>
        {
            if (Interlocked.Increment(ref sentCount) > 5)
            {
                await interruption.CancelAsync();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            return FakeHttpMessageHandler.Response(HttpStatusCode.OK);
        });
        var scenario = Scenario(load: """{ "concurrency": 2, "totalRequests": 1000 }""");

        var result = await RunAsync(handler, scenario, interruption.Token);

        Assert.True(result.Interrupted);
        // Requests in flight when Ctrl+C arrives are dropped, not counted as errors.
        Assert.InRange(result.Statistics.Overall.Count, 1, 5);
        Assert.Equal(0, result.Statistics.Overall.ErrorCount);
    }

    [Fact]
    public async Task Duration_StopsTheRunAfterTheDuration()
    {
        var handler = new FakeHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(5, cancellationToken);
            return FakeHttpMessageHandler.Response(HttpStatusCode.OK);
        });
        var scenario = Scenario(load: """{ "concurrency": 2, "durationSec": 1, "warmup": 2 }""");
        var progress = new CollectingProgress();

        var result = await RunAsync(handler, scenario, TestContext.Current.CancellationToken, progress: progress);

        Assert.InRange(result.Elapsed, TimeSpan.FromSeconds(0.9), TimeSpan.FromSeconds(10));
        Assert.Equal(handler.RequestCount - 2, result.Statistics.Overall.Count);
        Assert.False(result.Interrupted);
        Assert.NotEmpty(progress.Reports);
        Assert.Equal(handler.RequestCount, progress.Reports.Last().CompletedRequests);
        Assert.Equal(TimeSpan.FromSeconds(1), progress.Reports.Last().PlannedDuration);
    }

    [Fact]
    public async Task Thresholds_AreEvaluated()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.InternalServerError);
        var scenario = Scenario(load: """{ "concurrency": 1, "totalRequests": 2 }""", thresholds: """{ "errorRatePercent": 0 }""");

        var result = await RunAsync(handler, scenario, TestContext.Current.CancellationToken);

        var check = Assert.Single(result.ThresholdChecks);
        Assert.Equal(("errorRatePercent", 100.0, false), (check.Name, check.Actual!.Value, check.Passed));
        Assert.False(result.ThresholdsPassed);
    }

    private static CompiledScenario Scenario(string load, string? requests = null, string? thresholds = null)
    {
        requests ??= """{ "name": "health", "method": "GET", "path": "/health", "expect": { "status": [200] } }""";
        var thresholdsField = thresholds is null ? string.Empty : $""", "thresholds": {thresholds}""";
        var json = $$"""
            {
              "version": 1, "name": "test", "baseUrl": "http://localhost:5080",
              "load": {{load}},
              "requests": [ {{requests}} ]{{thresholdsField}}
            }
            """;
        var loadResult = TestScenarios.Load(json);
        return loadResult.Scenario ?? throw new InvalidOperationException(string.Join("; ", loadResult.Issues));
    }

    private static async Task<RunResult> RunAsync(
        HttpMessageHandler handler,
        CompiledScenario scenario,
        CancellationToken cancellationToken,
        SecretMasker? secretMasker = null,
        IProgress<RunProgress>? progress = null)
    {
        using var httpClient = new HttpClient(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
        var runner = new LoadRunner(httpClient, TimeProvider.System, Random.Shared, secretMasker ?? SecretMasker.CreateEmpty());
        var options = RunOptions.FromLoad(scenario.Scenario.Load, runId: null);
        return await runner.RunAsync(scenario, options, progress, cancellationToken);
    }

    private sealed class CollectingProgress : IProgress<RunProgress>
    {
        private readonly ConcurrentQueue<RunProgress> _reports = new();

        public IReadOnlyList<RunProgress> Reports => [.. _reports];

        public void Report(RunProgress value)
        {
            _reports.Enqueue(value);
        }
    }
}
