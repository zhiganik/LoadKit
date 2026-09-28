using System.Net;
using LoadKit.Core.Auth;
using LoadKit.Core.Engine;
using LoadKit.Core.Metrics;
using LoadKit.Core.Tests.TestSupport;

namespace LoadKit.Core.Tests.Engine;

public sealed class ScenarioCheckerTests
{
    [Fact]
    public async Task SendsOneRequestPerEntry_InOrder_AndMarksUnexpectedStatuses()
    {
        var sentPaths = new List<string>();
        var handler = new FakeHttpMessageHandler((request, _) =>
        {
            sentPaths.Add(request.RequestUri!.AbsolutePath);
            var statusCode = request.RequestUri.AbsolutePath == "/orders" ? HttpStatusCode.Unauthorized : HttpStatusCode.OK;
            return Task.FromResult(FakeHttpMessageHandler.Response(statusCode, "echo secret-value-1234 " + new string('x', 600)));
        });

        var results = await CheckAsync(handler);

        Assert.Equal(["/health", "/orders"], sentPaths);
        Assert.Equal([("health", 200, true), ("orders", 401, false)], results.Select(result => (result.Name, result.StatusCode, result.IsExpected)));
        Assert.StartsWith("echo *** xxx", results[0].BodyStart, StringComparison.Ordinal);
        Assert.EndsWith("...", results[0].BodyStart, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConnectionFailure_IsReported_WithoutStatus()
    {
        var handler = new FakeHttpMessageHandler((_, _) => throw new HttpRequestException(HttpRequestError.ConnectionError, "refused"));

        var results = await CheckAsync(handler);

        Assert.All(results, result => Assert.Equal((0, false, ErrorKind.Connection, "refused"), (result.StatusCode, result.IsExpected, result.Error, result.ErrorMessage)));
    }

    private static async Task<IReadOnlyList<RequestCheckResult>> CheckAsync(HttpMessageHandler handler)
    {
        var scenario = TestScenarios.Load("""
            {
              "version": 1, "name": "t", "baseUrl": "http://localhost:5080",
              "load": { "concurrency": 5, "totalRequests": 100 },
              "requests": [
                { "name": "health", "method": "GET", "path": "/health", "weight": 9, "expect": { "status": [200] } },
                { "name": "orders", "method": "GET", "path": "/orders", "expect": { "status": [200] } }
              ]
            }
            """).Scenario!;
        using var httpClient = new HttpClient(handler);
        var checker = new ScenarioChecker(httpClient, TimeProvider.System, new Random(1), new SecretMasker(["secret-value-1234"], []));
        return await checker.CheckAsync(scenario, TestContext.Current.CancellationToken);
    }
}
