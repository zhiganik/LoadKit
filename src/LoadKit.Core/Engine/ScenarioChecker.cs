using System.Diagnostics;
using LoadKit.Core.Auth;
using LoadKit.Core.Metrics;
using LoadKit.Core.Scenarios;
using LoadKit.Core.Scenarios.Templates;

namespace LoadKit.Core.Engine;

/// <summary>
/// <c>check</c>: sends one real request for each <c>requests[]</c> entry, in order, through the same pipeline as
/// <c>run</c>, and reports status, time and the start of the body.
/// </summary>
/// <param name="httpClient">From <see cref="HttpPipelineFactory.Create"/> with the initialized auth provider.</param>
public sealed class ScenarioChecker(HttpClient httpClient, TimeProvider timeProvider, Random random, SecretMasker secretMasker)
{
    public const int MaxBodyStartLength = 500;

    public async Task<IReadOnlyList<RequestCheckResult>> CheckAsync(CompiledScenario scenario, CancellationToken cancellationToken)
    {
        var requestFactory = new RequestFactory(scenario, new TemplateContext(timeProvider, random), runId: null);
        var timeout = TimeSpan.FromMilliseconds(scenario.Scenario.Load.TimeoutMs);
        var results = new List<RequestCheckResult>(scenario.Requests.Count);
        for (var requestIndex = 0; requestIndex < scenario.Requests.Count; requestIndex++)
        {
            results.Add(await CheckRequestAsync(requestFactory, scenario.Requests[requestIndex], requestIndex, timeout, cancellationToken));
        }

        return results;
    }

    private async Task<RequestCheckResult> CheckRequestAsync(
        RequestFactory requestFactory,
        CompiledRequest compiledRequest,
        int requestIndex,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var definition = compiledRequest.Definition;
        using var request = requestFactory.Create(requestIndex);
        var url = secretMasker.MaskText(request.RequestUri!.ToString());
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        var startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            using var response = await httpClient.SendAsync(request, timeoutSource.Token);
            var body = await response.Content.ReadAsStringAsync(timeoutSource.Token);
            var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
            var statusCode = (int)response.StatusCode;
            return new RequestCheckResult(
                definition.Name,
                definition.Method,
                url,
                statusCode,
                elapsed,
                definition.Expect.Status.Contains(statusCode),
                secretMasker.MaskText(StartOf(body)),
                ErrorKind.None,
                null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failed(definition.Name, definition.Method, url, startTimestamp, ErrorKind.Timeout, $"no response within {timeout.TotalMilliseconds} ms (load.timeoutMs)");
        }
        catch (HttpRequestException exception)
        {
            return Failed(definition.Name, definition.Method, url, startTimestamp, TransportErrorClassifier.Classify(exception), exception.Message);
        }
    }

    private RequestCheckResult Failed(string name, string method, string url, long startTimestamp, ErrorKind error, string message)
    {
        return new RequestCheckResult(
            name,
            method,
            url,
            0,
            Stopwatch.GetElapsedTime(startTimestamp),
            false,
            string.Empty,
            error,
            secretMasker.MaskText(message));
    }

    private static string StartOf(string body)
    {
        return body.Length <= MaxBodyStartLength ? body : string.Concat(body.AsSpan(0, MaxBodyStartLength), "…");
    }
}
