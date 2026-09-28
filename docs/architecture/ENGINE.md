# Load engine and metrics

> Status: draft

## Files

`Engine/`: `LoadRunner` (public entry) and `LoadRun` (state of one run, the worker loop), `WeightedRequestPicker`,
`RequestFactory`, `HttpPipelineFactory`, `RunOptions`, `RunProgress`, `RunResult`, `RunIdGenerator`.
`Metrics/`: `RequestResult`, `ResultCollector`, `ErrorSampleCollector`, `PercentileCalculator`,
`RunStatisticsCalculator`, `ThresholdEvaluator`; `HistogramBuilder` comes with reports (phase 4).

## Load model

Closed model: `concurrency` workers, each sends the next request right after receiving a response.
This means "N requests in flight", not "N requests per second". The open model is v2.

## Engine

- One `HttpClient` per run. `SocketsHttpHandler`: `MaxConnectionsPerServer = concurrency`,
  `PooledConnectionLifetime = 2 min`, automatic decompression.
- Workers are `Task`s. The next request number comes from `Interlocked.Increment`; they stop when
  `totalRequests` is reached or `durationSec` has elapsed.
- The first `warmup` numbers are sent but not included in metrics.
- Request selection: prefix sums of weights + binary search.
- `RequestFactory` prepares everything constant once (literal URLs and bodies, escaped query names, header placement);
  per request it only renders templates. With `tagRuns`, `loadrun=<id>` is appended to every URL.
- `Ctrl+C` → cancellation; the report is built from the collected data and marked "interrupted".
  Requests in flight at that moment are dropped, not counted as errors.
- Progress: `IProgress<RunProgress>` every 250 ms (sent, errors, current RPS).

## Measurement

- From `Stopwatch.GetTimestamp()` before `SendAsync` until the body is fully read.
- Not included: building the request, templates, the token.
- Timeout: a per-request `CancellationTokenSource`, classified as `Timeout`.

## Result collection

```csharp
readonly record struct RequestResult(
    int RequestIndex, int StatusCode, long ElapsedTicks, ErrorKind Error);
```

`RequestIndex` is the index in the scenario `requests[]`; `StatusCode` is `0` when there was no response;
`ElapsedTicks` are `Stopwatch` ticks.

- `totalRequests` known → the array is preallocated, lock-free write by request number.
- `durationSec` → each worker has its own `List<RequestResult>`, merged at the end.
- Sample errors: up to 5 per status code and `ErrorKind`, masked by `SecretMasker`. For an unexpected status —
  the first 2 KB of the body (captured only while the group has free slots); without a response — the exception message.

`ErrorKind`: `None`, `UnexpectedStatus` (status not in `expect.status`), `SlowResponse` (expected status, slower than
`expect.maxMs`), `Timeout`, `Connection`, `Tls`, `Other`.

## Percentiles

Nearest-rank: sort the durations; the P-th percentile is the element with rank `ceil(P/100 × N)`
(index `rank − 1`). For N = 2000: p50 is the 1000th, p95 the 1900th, p99 the 1980th.

The rank is computed **in integers**: `rank = (P × N + 99) / 100` for integer P. In `double`,
`0.95 × 2000` may give `1900.0000000000002`, and `ceil` returns 1901 — one request too far.

- Computed per scenario request and for all requests together.
- Latency uses only requests that received a response: a timeout or a refused connection has no response time.
  They still count in the request count and the error rate.
- Percentiles are not averaged across groups; the total is computed from all raw data.
- RPS = number of counted requests / main run duration.

## Thresholds

`ThresholdEvaluator` compares the overall p50/p95/p99 and the error rate with `thresholds`.
The result is a list of checks (threshold, actual, ok/fail) for the report and the exit code.
A value equal to the limit passes. A latency threshold with no responses at all fails (actual "n/a").

## Accuracy limits

The tester and the API on the same machine share the CPU — the numbers are good for "before/after" comparison, not as absolutes.
If the tester's CPU load exceeds 85%, the report contains a warning.
