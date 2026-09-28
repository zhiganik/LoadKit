using System.Globalization;
using LoadKit.Core.Engine;
using Spectre.Console;

namespace LoadKit.Cli.Rendering;

/// <summary>Shows <see cref="RunProgress"/> in a Spectre progress bar: requests for a count run, seconds for a duration run.</summary>
internal sealed class ProgressTaskReporter(ProgressTask task) : IProgress<RunProgress>
{
    public void Report(RunProgress value)
    {
        if (value.PlannedRequests is { } plannedRequests)
        {
            task.MaxValue = plannedRequests;
            task.Value = value.CompletedRequests;
        }
        else if (value.PlannedDuration is { } plannedDuration)
        {
            task.MaxValue = plannedDuration.TotalSeconds;
            task.Value = Math.Min(value.Elapsed.TotalSeconds, plannedDuration.TotalSeconds);
        }

        var requestsPerSecond = value.RequestsPerSecond.ToString("0", CultureInfo.InvariantCulture);
        task.Description = $"{value.CompletedRequests} sent · {value.Errors} errors · {requestsPerSecond} rps";
    }
}
