using System.Text.Json;
using LoadKit.Core.Auth;
using LoadKit.Core.Metrics;
using LoadKit.Core.Reporting;
using LoadKit.Core.Scenarios;
using LoadKit.Core.Tests.TestSupport;
using static LoadKit.Core.Tests.TestSupport.TestRunResults;

namespace LoadKit.Core.Tests.Reporting;

public sealed class ReportWriterTests
{
    [Fact]
    public void Markdown_HasStableSections_AndTheKqlQuery()
    {
        var markdown = MarkdownReportWriter.Write(SampleReport());

        foreach (var heading in new[]
        {
            "# Load test report: orders|mix", "## Summary", "## Warnings", "## Thresholds", "## Requests",
            "## Status codes", "## Errors", "## Latency histogram", "## Sample errors", "## Application Insights",
        })
        {
            Assert.Contains(heading + "\n", markdown, StringComparison.Ordinal);
        }

        Assert.Contains("| Result | thresholds FAILED |", markdown, StringComparison.Ordinal);
        Assert.Contains("| p95Ms | 1 | 190 | FAIL |", markdown, StringComparison.Ordinal);
        Assert.Contains("```kusto\nrequests\n", markdown, StringComparison.Ordinal);
        Assert.Contains("### 500, create (UnexpectedStatus)\n\n````text\n{\"error\":\"db down\"}\n````", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Markdown_EscapesPipesInTableCells()
    {
        var markdown = MarkdownReportWriter.Write(SampleReport());

        Assert.Contains("| Base URL | http://localhost:5080/a\\|b |", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Json_RoundTrips_WithCamelCaseAndExplicitNulls()
    {
        var report = SampleReport();

        var json = JsonReportWriter.Write(report);
        using var document = JsonDocument.Parse(json);

        Assert.Equal(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("overall").GetProperty("method").ValueKind);
        Assert.Equal("UnexpectedStatus", document.RootElement.GetProperty("errorSamples")[0].GetProperty("error").GetString());
        Assert.Equal(JsonReportWriter.Write(JsonReportWriter.Read(json)!), json);
    }

    [Fact]
    public void FolderName_IsTimestampAndSafeScenarioName()
    {
        Assert.Equal("20260928-101530-orders-mix", ReportFileWriter.FolderName(SampleReport()));
    }

    [Fact]
    public async Task FileWriter_WritesBothFiles()
    {
        using var directory = new TemporaryDirectory();

        var folder = await ReportFileWriter.WriteAsync(SampleReport(), directory.Path, TestContext.Current.CancellationToken);

        Assert.Equal(Path.Combine(directory.Path, "20260928-101530-orders-mix"), folder);
        Assert.StartsWith("# Load test report", await File.ReadAllTextAsync(Path.Combine(folder, "report.md"), TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.NotNull(JsonReportWriter.Read(await File.ReadAllTextAsync(Path.Combine(folder, "report.json"), TestContext.Current.CancellationToken)));
    }

    /// <summary>A report with every optional part present: warnings, errors, samples, thresholds, KQL.</summary>
    internal static RunReport SampleReport()
    {
        var scenario = TestScenarios.Load("""
            {
              "version": 1, "name": "orders|mix", "description": "Reads and creates", "baseUrl": "http://localhost:5080/a|b",
              "auth": { "type": "bearer", "token": "${env:T}" },
              "load": { "concurrency": 4, "totalRequests": 220, "warmup": 20 },
              "requests": [
                { "name": "list", "method": "GET", "path": "/api/orders", "expect": { "status": [200] } },
                { "name": "create", "method": "POST", "path": "/api/orders", "body": { "id": "{{guid}}" }, "expect": { "status": [201] } }
              ],
              "thresholds": { "p95Ms": 1, "errorRatePercent": 50 }
            }
            """, new Dictionary<string, string> { ["T"] = "token-value" }).Scenario!;
        var results = Enumerable.Range(1, 200)
            .Select(index => index % 10 == 0 ? Response(1, 500, index, ErrorKind.UnexpectedStatus) : Response(0, 200, index))
            .Append(new RequestResult(0, 0, 0, ErrorKind.Timeout))
            .ToArray();
        ErrorSample[] samples = [new("create", 500, ErrorKind.UnexpectedStatus, """{"error":"db down"}""")];
        var result = TestRunResults.Create(scenario, results, testerCpuPercent: 90, errorSamples: samples);
        return RunReportBuilder.Build(scenario, result, "1.0.0", SecretMasker.CreateEmpty(), authProvider: null);
    }
}
