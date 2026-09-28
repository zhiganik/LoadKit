using System.Globalization;

namespace LoadKit.Core.Reporting;

/// <summary>KQL for the Application Insights <c>requests</c> table: this run's requests by <c>loadrun</c>, per operation name.</summary>
public static class ApplicationInsightsQuery
{
    /// <summary>Margin around the run for the timestamp filter: clocks and ingestion are not exact.</summary>
    public static readonly TimeSpan TimeMargin = TimeSpan.FromMinutes(5);

    public static string Build(string runId, DateTimeOffset startedAt, TimeSpan elapsed)
    {
        var from = FormatTimestamp(startedAt - TimeMargin);
        var to = FormatTimestamp(startedAt + elapsed + TimeMargin);
        return $"""
            requests
            | where timestamp between (datetime({from}) .. datetime({to}))
            | where url contains "loadrun={runId}"
            | summarize requests = count(), failures = countif(success == false),
                p50 = percentile(duration, 50), p95 = percentile(duration, 95), p99 = percentile(duration, 99) by name
            | order by requests desc
            """;
    }

    private static string FormatTimestamp(DateTimeOffset timestamp)
    {
        return timestamp.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }
}
