using System.Globalization;
using LoadKit.Core.Engine;

namespace LoadKit.Cli.Rendering;

internal static class RunPlanText
{
    /// <summary>For example "concurrency 10, 1000 requests (warmup 20)" or "concurrency 10, 60 s".</summary>
    public static string Describe(RunOptions options)
    {
        var volume = options.TotalRequests is { } totalRequests
            ? $"{totalRequests} requests"
            : $"{options.Duration!.Value.TotalSeconds.ToString(CultureInfo.InvariantCulture)} s";
        var warmup = options.Warmup > 0 ? $" (warmup {options.Warmup})" : string.Empty;
        return $"concurrency {options.Concurrency}, {volume}{warmup}";
    }
}
