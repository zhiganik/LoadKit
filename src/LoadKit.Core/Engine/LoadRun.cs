using System.Diagnostics;
using System.Globalization;
using System.Text;
using LoadKit.Core.Auth;
using LoadKit.Core.Metrics;
using LoadKit.Core.Scenarios;
using LoadKit.Core.Scenarios.Templates;

namespace LoadKit.Core.Engine;

/// <summary>State of one run. The worker loop is the hot path: no locks, no LINQ, counters via <see cref="Interlocked"/>.</summary>
internal sealed class LoadRun
{
    private const int MaxErrorSamplesPerGroup = 5;
    private const int MaxErrorSampleBytes = 2048;
    private const int ReadBufferSize = 16 * 1024;
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(250);

    private readonly HttpClient _httpClient;
    private readonly CompiledScenario _scenario;
    private readonly RunOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly Random _random;
    private readonly SecretMasker _secretMasker;
    private readonly IProgress<RunProgress>? _progress;
    private readonly RequestFactory _requestFactory;
    private readonly WeightedRequestPicker _requestPicker;
    private readonly string[] _requestNames;
    private readonly int[][] _expectedStatuses;
    private readonly long[] _maxResponseTicks;
    private readonly ResultCollector _results;
    private readonly ErrorSampleCollector _errorSamples = new(MaxErrorSamplesPerGroup);

    private long _issuedRequests;
    private long _completedRequests;
    private long _measuredErrors;
    private long _runStartTimestamp;
    private long _measuredStartTimestamp;
    private volatile bool _durationElapsed;

    // Touched only by the progress timer callback.
    private long _lastProgressCompleted;
    private long _lastProgressTimestamp;

    public LoadRun(
        HttpClient httpClient,
        CompiledScenario scenario,
        RunOptions options,
        TimeProvider timeProvider,
        Random random,
        SecretMasker secretMasker,
        IProgress<RunProgress>? progress)
    {
        _httpClient = httpClient;
        _scenario = scenario;
        _options = options;
        _timeProvider = timeProvider;
        _random = random;
        _secretMasker = secretMasker;
        _progress = progress;
        _requestFactory = new RequestFactory(scenario, new TemplateContext(timeProvider, random), options.RunId);

        var requestCount = scenario.Requests.Count;
        _requestNames = new string[requestCount];
        _expectedStatuses = new int[requestCount][];
        _maxResponseTicks = new long[requestCount];
        var weights = new int[requestCount];
        for (var index = 0; index < requestCount; index++)
        {
            var definition = scenario.Requests[index].Definition;
            _requestNames[index] = definition.Name;
            _expectedStatuses[index] = [.. definition.Expect.Status];
            _maxResponseTicks[index] = definition.Expect.MaxMs is { } maxMs ? maxMs * Stopwatch.Frequency / 1000 : long.MaxValue;
            weights[index] = definition.Weight;
        }

        _requestPicker = new WeightedRequestPicker(weights);
        _results = options.TotalRequests is { } totalRequests
            ? ResultCollector.ForRequestCount(totalRequests - options.Warmup)
            : ResultCollector.ForDuration(options.Concurrency);
    }

    public async Task<RunResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        var startedAt = _timeProvider.GetUtcNow();
        var startProcessorTime = TryGetProcessorTime();
        _runStartTimestamp = Stopwatch.GetTimestamp();
        _lastProgressTimestamp = _runStartTimestamp;

        using var durationTimer = _options.Duration is { } duration
            ? _timeProvider.CreateTimer(_ => _durationElapsed = true, null, duration, Timeout.InfiniteTimeSpan)
            : null;
        var progressTimer = _progress is null
            ? null
            : _timeProvider.CreateTimer(_ => ReportProgress(), null, ProgressInterval, ProgressInterval);

        var workers = new Task[_options.Concurrency];
        for (var workerIndex = 0; workerIndex < workers.Length; workerIndex++)
        {
            var currentWorkerIndex = workerIndex;
            workers[workerIndex] = Task.Run(() => RunWorkerAsync(currentWorkerIndex, cancellationToken), CancellationToken.None);
        }

        await Task.WhenAll(workers);
        var endTimestamp = Stopwatch.GetTimestamp();

        if (progressTimer is not null)
        {
            await progressTimer.DisposeAsync();
            ReportProgress();
        }

        var testerCpuPercent = CalculateCpuPercent(startProcessorTime, TryGetProcessorTime(), Stopwatch.GetElapsedTime(_runStartTimestamp, endTimestamp));
        return BuildResult(startedAt, endTimestamp, cancellationToken.IsCancellationRequested, testerCpuPercent);
    }

    private static TimeSpan? TryGetProcessorTime()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return process.TotalProcessorTime;
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException or PlatformNotSupportedException)
        {
            return null;
        }
    }

    private static double? CalculateCpuPercent(TimeSpan? startProcessorTime, TimeSpan? endProcessorTime, TimeSpan elapsed)
    {
        if (startProcessorTime is not { } start || endProcessorTime is not { } end || elapsed <= TimeSpan.Zero)
        {
            return null;
        }

        return (end - start).TotalMilliseconds * 100 / (elapsed.TotalMilliseconds * Environment.ProcessorCount);
    }

    private async Task RunWorkerAsync(int workerIndex, CancellationToken runToken)
    {
        var readBuffer = new byte[ReadBufferSize];
        var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(runToken);
        try
        {
            while (!runToken.IsCancellationRequested && !_durationElapsed)
            {
                var sequenceNumber = Interlocked.Increment(ref _issuedRequests);
                if (sequenceNumber > _options.TotalRequests)
                {
                    break;
                }

                var isMeasured = sequenceNumber > _options.Warmup;
                if (sequenceNumber == _options.Warmup + 1)
                {
                    Interlocked.CompareExchange(ref _measuredStartTimestamp, Stopwatch.GetTimestamp(), 0);
                }

                // Reuse the source while it has not fired; after a timeout it cannot be reset.
                if (!timeoutSource.TryReset())
                {
                    timeoutSource.Dispose();
                    timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(runToken);
                }

                timeoutSource.CancelAfter(_options.RequestTimeout);

                var requestIndex = _requestPicker.Pick(_random);
                var result = await SendAsync(requestIndex, readBuffer, isMeasured, timeoutSource.Token, runToken);
                if (result is not { } completedResult)
                {
                    break;
                }

                Interlocked.Increment(ref _completedRequests);
                if (isMeasured)
                {
                    _results.Record(workerIndex, sequenceNumber - _options.Warmup - 1, completedResult);
                    if (completedResult.IsError)
                    {
                        Interlocked.Increment(ref _measuredErrors);
                    }
                }
            }
        }
        finally
        {
            timeoutSource.Dispose();
        }
    }

    /// <returns>Null when the run was cancelled while the request was in flight; such a request is not counted.</returns>
    private async ValueTask<RequestResult?> SendAsync(
        int requestIndex,
        byte[] readBuffer,
        bool isMeasured,
        CancellationToken requestToken,
        CancellationToken runToken)
    {
        using var request = _requestFactory.Create(requestIndex);
        var startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, requestToken);
            var statusCode = (int)response.StatusCode;
            var isExpectedStatus = IsExpectedStatus(requestIndex, statusCode);
            var sampleSlot = -1;
            if (!isExpectedStatus && isMeasured && _errorSamples.TryReserve(statusCode, ErrorKind.UnexpectedStatus, out var reservedSlot))
            {
                sampleSlot = reservedSlot;
            }

            var capturedBody = await ReadBodyAsync(response.Content, readBuffer, captureStart: sampleSlot >= 0, requestToken);
            var elapsedTicks = Stopwatch.GetTimestamp() - startTimestamp;
            if (sampleSlot >= 0)
            {
                StoreSample(sampleSlot, requestIndex, statusCode, ErrorKind.UnexpectedStatus, capturedBody!);
            }

            var error = !isExpectedStatus ? ErrorKind.UnexpectedStatus
                : elapsedTicks > _maxResponseTicks[requestIndex] ? ErrorKind.SlowResponse
                : ErrorKind.None;
            return new RequestResult(requestIndex, statusCode, elapsedTicks, error);
        }
        catch (OperationCanceledException) when (runToken.IsCancellationRequested)
        {
            return null;
        }
        catch (OperationCanceledException) when (requestToken.IsCancellationRequested)
        {
            var timeoutMs = _options.RequestTimeout.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
            return Failed(requestIndex, startTimestamp, ErrorKind.Timeout, isMeasured, $"no complete response within {timeoutMs} ms (load.timeoutMs)");
        }
        catch (Exception exception)
        {
            return Failed(requestIndex, startTimestamp, TransportErrorClassifier.Classify(exception), isMeasured, DescribeException(exception));
        }
    }

    private RequestResult Failed(int requestIndex, long startTimestamp, ErrorKind error, bool isMeasured, string description)
    {
        var elapsedTicks = Stopwatch.GetTimestamp() - startTimestamp;
        if (isMeasured && _errorSamples.TryReserve(0, error, out var sampleSlot))
        {
            StoreSample(sampleSlot, requestIndex, 0, error, description);
        }

        return new RequestResult(requestIndex, 0, elapsedTicks, error);
    }

    private void StoreSample(int slot, int requestIndex, int statusCode, ErrorKind error, string text)
    {
        _errorSamples.Store(slot, new ErrorSample(_requestNames[requestIndex], statusCode, error, _secretMasker.MaskText(text)));
    }

    private bool IsExpectedStatus(int requestIndex, int statusCode)
    {
        var expectedStatuses = _expectedStatuses[requestIndex];
        for (var index = 0; index < expectedStatuses.Length; index++)
        {
            if (expectedStatuses[index] == statusCode)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reads the whole body (timing ends after it); keeps its first 2 KB when <paramref name="captureStart"/>.</summary>
    private static async ValueTask<string?> ReadBodyAsync(
        HttpContent content,
        byte[] readBuffer,
        bool captureStart,
        CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        var captured = captureStart ? new byte[MaxErrorSampleBytes] : null;
        var capturedLength = 0;
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(readBuffer, cancellationToken)) > 0)
        {
            if (captured is not null && capturedLength < captured.Length)
            {
                var copyLength = Math.Min(bytesRead, captured.Length - capturedLength);
                readBuffer.AsSpan(0, copyLength).CopyTo(captured.AsSpan(capturedLength));
                capturedLength += copyLength;
            }
        }

        return captured is null ? null : Encoding.UTF8.GetString(captured, 0, capturedLength);
    }

    private static string DescribeException(Exception exception)
    {
        var innerMessage = exception.InnerException?.Message;
        return innerMessage is null || exception.Message.Contains(innerMessage, StringComparison.Ordinal)
            ? $"{exception.GetType().Name}: {exception.Message}"
            : $"{exception.GetType().Name}: {exception.Message} ({innerMessage})";
    }

    private void ReportProgress()
    {
        var now = Stopwatch.GetTimestamp();
        var completed = Interlocked.Read(ref _completedRequests);
        var interval = Stopwatch.GetElapsedTime(_lastProgressTimestamp, now);
        var requestsPerSecond = interval > TimeSpan.Zero ? (completed - _lastProgressCompleted) / interval.TotalSeconds : 0;
        _lastProgressCompleted = completed;
        _lastProgressTimestamp = now;

        _progress!.Report(new RunProgress(
            completed,
            Interlocked.Read(ref _measuredErrors),
            Stopwatch.GetElapsedTime(_runStartTimestamp, now),
            requestsPerSecond,
            _options.TotalRequests,
            _options.Duration));
    }

    private RunResult BuildResult(DateTimeOffset startedAt, long endTimestamp, bool interrupted, double? testerCpuPercent)
    {
        var results = _results.ToArray();
        var measuredStartTimestamp = Interlocked.Read(ref _measuredStartTimestamp);
        var measuredDuration = measuredStartTimestamp == 0 ? TimeSpan.Zero : Stopwatch.GetElapsedTime(measuredStartTimestamp, endTimestamp);
        var statistics = RunStatisticsCalculator.Calculate(results, _requestNames, measuredDuration);

        return new RunResult(
            _scenario.Scenario.Name,
            _options,
            startedAt,
            Stopwatch.GetElapsedTime(_runStartTimestamp, endTimestamp),
            interrupted,
            statistics,
            ThresholdEvaluator.Evaluate(_scenario.Scenario.Thresholds, statistics.Overall),
            _errorSamples.ToList(),
            results,
            testerCpuPercent);
    }
}
