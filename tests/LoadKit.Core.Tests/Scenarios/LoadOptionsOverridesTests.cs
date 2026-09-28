using LoadKit.Core.Scenarios;
using LoadKit.Core.Scenarios.Model;

namespace LoadKit.Core.Tests.Scenarios;

public sealed class LoadOptionsOverridesTests
{
    private static readonly LoadOptions CountLoad = new(Concurrency: 5, TotalRequests: 100, DurationSec: null, Warmup: 10, TimeoutMs: 30_000);

    [Fact]
    public void NoOverrides_KeepLoad()
    {
        var overrides = new LoadOptionsOverrides(null, null, null);

        Assert.Empty(overrides.Validate(CountLoad));
        Assert.Equal(CountLoad, overrides.ApplyTo(CountLoad));
    }

    [Fact]
    public void Duration_ReplacesTotalRequests()
    {
        var overrides = new LoadOptionsOverrides(Concurrency: 20, TotalRequests: null, DurationSec: 60);

        Assert.Empty(overrides.Validate(CountLoad));
        Assert.Equal(CountLoad with { Concurrency = 20, TotalRequests = null, DurationSec = 60 }, overrides.ApplyTo(CountLoad));
    }

    [Fact]
    public void Total_ReplacesDuration()
    {
        var durationLoad = CountLoad with { TotalRequests = null, DurationSec = 30 };
        var overrides = new LoadOptionsOverrides(null, TotalRequests: 500, DurationSec: null);

        Assert.Equal(durationLoad with { TotalRequests = 500, DurationSec = null }, overrides.ApplyTo(durationLoad));
    }

    [Fact]
    public void TotalAndDurationTogether_IsLoadModeError()
    {
        var issue = Assert.Single(new LoadOptionsOverrides(null, 10, 10).Validate(CountLoad));

        Assert.Equal((ValidationSeverity.Error, ValidationCodes.LoadMode, "--total"), (issue.Severity, issue.Code, issue.Path));
    }

    [Theory]
    [InlineData(0, null, null, "--concurrency")]
    [InlineData(null, -1, null, "--total")]
    [InlineData(null, null, 0, "--duration")]
    public void NonPositiveValues_AreInvalid(int? concurrency, int? totalRequests, int? durationSec, string expectedPath)
    {
        var issue = Assert.Single(new LoadOptionsOverrides(concurrency, totalRequests, durationSec).Validate(CountLoad));

        Assert.Equal((ValidationCodes.InvalidValue, expectedPath), (issue.Code, issue.Path));
    }

    [Fact]
    public void TotalNotAboveWarmup_IsError()
    {
        var issue = Assert.Single(new LoadOptionsOverrides(null, 10, null).Validate(CountLoad));

        Assert.Equal((ValidationSeverity.Error, ValidationCodes.WarmupGreaterThanTotal), (issue.Severity, issue.Code));
    }

    [Fact]
    public void ConcurrencyAboveTotal_IsWarning()
    {
        var issue = Assert.Single(new LoadOptionsOverrides(200, null, null).Validate(CountLoad));

        Assert.Equal((ValidationSeverity.Warning, ValidationCodes.ConcurrencyGreaterThanTotal), (issue.Severity, issue.Code));
    }
}
