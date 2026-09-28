using LoadKit.Core.Auth;
using LoadKit.Core.Engine;
using LoadKit.Core.Metrics;
using LoadKit.Core.Reporting;
using LoadKit.Core.Scenarios;
using LoadKit.Core.Tests.TestSupport;
using static LoadKit.Core.Tests.TestSupport.TestRunResults;

namespace LoadKit.Core.Tests.Reporting;

public sealed class RunReportBuilderTests
{
    [Fact]
    public void Report_CarriesStatisticsInMilliseconds_PerRequestAndOverall()
    {
        var scenario = Scenario();
        var results = Enumerable.Range(1, 200).Select(index => Response(index % 2, 200, index)).ToArray();

        var report = Build(scenario, TestRunResults.Create(scenario, results));

        Assert.Equal(RunReport.CurrentSchemaVersion, report.SchemaVersion);
        Assert.Equal(("list", "GET", "/api/orders"), (report.Requests[1].Name, report.Requests[1].Method, report.Requests[1].Path));
        Assert.Equal((null, null), (report.Overall.Method, report.Overall.Path));
        Assert.Equal(200, report.Overall.Count);
        Assert.Equal(100, report.Overall.LatencyMs!.P50);
        Assert.Equal(20, report.Overall.RequestsPerSecond);
        Assert.Equal((10.0, 12.0), (report.Run.MeasuredSeconds, report.Run.ElapsedSeconds));
        Assert.True(report.ThresholdsPassed);
        Assert.NotEmpty(report.Histogram);
        Assert.Empty(report.Warnings);
    }

    [Fact]
    public void BaseUrlAndPath_AreMasked()
    {
        var scenario = TestScenarios.Load("""
            {
              "version": 1, "name": "t", "baseUrl": "http://localhost:5080/${env:TENANT}",
              "load": { "concurrency": 1, "totalRequests": 1 },
              "requests": [ { "name": "r", "method": "GET", "path": "/items/${env:TENANT}", "expect": { "status": [200] } } ]
            }
            """, new Dictionary<string, string> { ["TENANT"] = "tenant-secret" }).Scenario!;

        var report = RunReportBuilder.Build(scenario, TestRunResults.Create(scenario, []), "1.0.0", new SecretMasker(["tenant-secret"], []), null);

        Assert.Equal("http://localhost:5080/***", report.Scenario.BaseUrl);
        Assert.Equal("/items/***", report.Requests[0].Path);
    }

    [Fact]
    public void ApplicationInsights_IsPresentOnlyForTaggedRuns()
    {
        var scenario = Scenario();

        var tagged = Build(scenario, TestRunResults.Create(scenario, [], runId: "run-42"));
        var untagged = Build(scenario, TestRunResults.Create(scenario, [], runId: null));

        Assert.Equal("run-42", tagged.ApplicationInsights!.RunId);
        Assert.Contains("url contains \"loadrun=run-42\"", tagged.ApplicationInsights.Kql, StringComparison.Ordinal);
        Assert.Contains("datetime(2026-09-28T10:10:30Z) .. datetime(2026-09-28T10:20:42Z)", tagged.ApplicationInsights.Kql, StringComparison.Ordinal);
        Assert.Null(untagged.ApplicationInsights);
    }

    [Fact]
    public void Warnings_CoverInterruptionFewRequestsAndCpu()
    {
        var scenario = Scenario();

        var report = Build(scenario, TestRunResults.Create(scenario, [Response(0, 200, 1)], interrupted: true, testerCpuPercent: 91.4));

        Assert.Equal(
            [ReportWarningCodes.Interrupted, ReportWarningCodes.TesterCpu, ReportWarningCodes.FewRequests],
            report.Warnings.Select(warning => warning.Code));
        Assert.Contains("1 of 980 planned", report.Warnings[0].Message, StringComparison.Ordinal);
        Assert.Contains("91% CPU", report.Warnings[1].Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, "the scenario has no auth")]
    [InlineData("""{ "type": "bearer", "token": "${env:T}" }""", "update the variable in .env")]
    [InlineData("""{ "type": "azureIdentity", "scope": "api://x/.default" }""", "check the scope")]
    public void UnexpectedUnauthorized_WarningDependsOnAuth(string? auth, string expectedAdvice)
    {
        var scenario = Scenario(auth);
        var results = Enumerable.Range(0, 150).Select(_ => Response(0, 401, 1, ErrorKind.UnexpectedStatus)).ToArray();

        var report = Build(scenario, TestRunResults.Create(scenario, results));

        var warning = Assert.Single(report.Warnings);
        Assert.Equal(ReportWarningCodes.UnauthorizedResponses, warning.Code);
        Assert.StartsWith("150 responses were an unexpected 401", warning.Message, StringComparison.Ordinal);
        Assert.Contains(expectedAdvice, warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NoThresholds_GivesNullVerdict()
    {
        var scenario = Scenario(thresholds: false);

        Assert.Null(Build(scenario, TestRunResults.Create(scenario, [])).ThresholdsPassed);
    }

    private static RunReport Build(CompiledScenario scenario, RunResult result)
    {
        return RunReportBuilder.Build(scenario, result, "1.0.0", SecretMasker.CreateEmpty(), authProvider: null);
    }

    private static CompiledScenario Scenario(string? auth = null, bool thresholds = true)
    {
        var authField = auth is null ? string.Empty : $""" "auth": {auth},""";
        var thresholdsField = thresholds ? """, "thresholds": { "p95Ms": 500 }""" : string.Empty;
        return TestScenarios.Load($$"""
            {
              "version": 1, "name": "orders", "baseUrl": "http://localhost:5080",{{authField}}
              "load": { "concurrency": 4, "totalRequests": 1000, "warmup": 20 },
              "requests": [
                { "name": "health", "method": "GET", "path": "/health", "expect": { "status": [200] } },
                { "name": "list", "method": "GET", "path": "/api/orders", "expect": { "status": [200] } }
              ]{{thresholdsField}}
            }
            """, new Dictionary<string, string> { ["T"] = "token-value" }).Scenario!;
    }
}
