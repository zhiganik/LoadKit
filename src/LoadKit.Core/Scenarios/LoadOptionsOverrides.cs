using LoadKit.Core.Scenarios.Model;

namespace LoadKit.Core.Scenarios;

/// <summary>
/// <c>run --concurrency/--total/--duration</c>. <c>--total</c> or <c>--duration</c> replaces the load mode of the scenario.
/// Issue paths name the CLI flag.
/// </summary>
public sealed record LoadOptionsOverrides(int? Concurrency, int? TotalRequests, int? DurationSec)
{
    public const string ConcurrencyFlag = "--concurrency";
    public const string TotalRequestsFlag = "--total";
    public const string DurationFlag = "--duration";

    public IReadOnlyList<ValidationIssue> Validate(LoadOptions load)
    {
        var issues = new List<ValidationIssue>();
        AddIfNotPositive(issues, ConcurrencyFlag, Concurrency);
        AddIfNotPositive(issues, TotalRequestsFlag, TotalRequests);
        AddIfNotPositive(issues, DurationFlag, DurationSec);
        if (TotalRequests.HasValue && DurationSec.HasValue)
        {
            issues.Add(ValidationIssue.Error(
                ValidationCodes.LoadMode,
                TotalRequestsFlag,
                "specify either --total or --duration, not both",
                "keep only one of the two flags"));
        }

        if (issues.Count > 0)
        {
            return issues;
        }

        // Without overrides of these values the scenario validator has already reported the same issues.
        var effective = ApplyTo(load);
        if (effective.TotalRequests is { } totalRequests && (Concurrency.HasValue || TotalRequests.HasValue))
        {
            AddTotalRequestsIssues(issues, effective, totalRequests);
        }

        return issues;
    }

    /// <summary>Call only when <see cref="Validate"/> returned no errors.</summary>
    public LoadOptions ApplyTo(LoadOptions load)
    {
        var effective = load with { Concurrency = Concurrency ?? load.Concurrency };
        if (TotalRequests is { } totalRequests)
        {
            return effective with { TotalRequests = totalRequests, DurationSec = null };
        }

        if (DurationSec is { } durationSec)
        {
            return effective with { TotalRequests = null, DurationSec = durationSec };
        }

        return effective;
    }

    private static void AddIfNotPositive(List<ValidationIssue> issues, string flag, int? value)
    {
        if (value is < 1)
        {
            issues.Add(ValidationIssue.Error(ValidationCodes.InvalidValue, flag, $"{flag} must be at least 1, got {value}"));
        }
    }

    private static void AddTotalRequestsIssues(List<ValidationIssue> issues, LoadOptions effective, int totalRequests)
    {
        if (effective.Warmup >= totalRequests)
        {
            issues.Add(ValidationIssue.Error(
                ValidationCodes.WarmupGreaterThanTotal,
                TotalRequestsFlag,
                $"load.warmup ({effective.Warmup}) must be less than the total number of requests ({totalRequests})",
                "pass a larger --total or lower load.warmup in the scenario"));
        }

        if (effective.Concurrency > totalRequests)
        {
            issues.Add(ValidationIssue.Warning(
                ValidationCodes.ConcurrencyGreaterThanTotal,
                ConcurrencyFlag,
                $"concurrency ({effective.Concurrency}) is greater than the total number of requests ({totalRequests})",
                "only that many workers will ever be busy"));
        }
    }
}
