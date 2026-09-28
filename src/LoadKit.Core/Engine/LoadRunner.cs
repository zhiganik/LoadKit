using LoadKit.Core.Auth;
using LoadKit.Core.Scenarios;

namespace LoadKit.Core.Engine;

/// <summary>
/// Runs the closed load model: <c>concurrency</c> workers, each sends the next request right after the previous
/// response. See docs/architecture/ENGINE.md.
/// </summary>
/// <param name="httpClient">From <see cref="HttpPipelineFactory"/>; tests pass a client over a fake handler.</param>
/// <param name="random">Shared by workers, so it must be thread-safe (<see cref="Random.Shared"/>).</param>
/// <param name="secretMasker">Applied to error samples.</param>
public sealed class LoadRunner(HttpClient httpClient, TimeProvider timeProvider, Random random, SecretMasker secretMasker)
{
    /// <summary>
    /// Runs until <see cref="RunOptions.TotalRequests"/> are sent or <see cref="RunOptions.Duration"/> has elapsed.
    /// Cancellation does not throw: the result covers the collected data and is marked interrupted.
    /// </summary>
    public Task<RunResult> RunAsync(
        CompiledScenario scenario,
        RunOptions options,
        IProgress<RunProgress>? progress,
        CancellationToken cancellationToken)
    {
        ValidateOptions(options);
        var run = new LoadRun(httpClient, scenario, options, timeProvider, random, secretMasker, progress);
        return run.ExecuteAsync(cancellationToken);
    }

    private static void ValidateOptions(RunOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Concurrency, 1, nameof(options));
        ArgumentOutOfRangeException.ThrowIfNegative(options.Warmup, nameof(options));
        if (options.TotalRequests.HasValue == options.Duration.HasValue)
        {
            throw new ArgumentException("Exactly one of TotalRequests and Duration must be set.", nameof(options));
        }

        if (options.TotalRequests is { } totalRequests && options.Warmup >= totalRequests)
        {
            throw new ArgumentException("Warmup must be less than TotalRequests.", nameof(options));
        }
    }
}
