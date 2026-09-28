using LoadKit.Core.Auth;
using LoadKit.Core.Engine;
using LoadKit.Core.Scenarios.Templates;
using LoadKit.Core.Tests.TestSupport;

namespace LoadKit.Core.Tests.Engine;

public sealed class RequestFactoryTests
{
    [Fact]
    public void Url_KeepsBasePath_EscapesQuery_AndAppendsRunId()
    {
        var factory = CreateFactory("""
            {
              "version": 1, "name": "t", "baseUrl": "http://localhost:5080/base/",
              "load": { "concurrency": 1, "totalRequests": 1 },
              "requests": [ { "name": "search", "method": "GET", "path": "/api/items",
                "query": { "q": "a b&c", "page": "{{seq}}" }, "expect": { "status": [200] } } ]
            }
            """, runId: "run-1");

        using var first = factory.Create(0);
        using var second = factory.Create(0);

        Assert.Equal("http://localhost:5080/base/api/items?q=a%20b%26c&page=1&loadrun=run-1", first.RequestUri!.AbsoluteUri);
        Assert.Equal("http://localhost:5080/base/api/items?q=a%20b%26c&page=2&loadrun=run-1", second.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public void Url_WithoutRunId_HasNoTag()
    {
        var factory = CreateFactory(TestScenarios.MinimalJson, runId: null);

        using var request = factory.Create(0);

        Assert.Equal("http://localhost:5080/health", request.RequestUri!.AbsoluteUri);
        Assert.Equal(HttpMethod.Get, request.Method);
    }

    [Fact]
    public async Task Body_IsRenderedPerRequest_WithContentTypeAndHeaders()
    {
        var factory = CreateFactory("""
            {
              "version": 1, "name": "t", "baseUrl": "http://localhost:5080",
              "headers": { "Accept": "application/json", "X-Trace": "root" },
              "load": { "concurrency": 1, "totalRequests": 1 },
              "requests": [ { "name": "create", "method": "POST", "path": "/orders",
                "headers": { "X-Trace": "req-{{seq}}" },
                "body": { "id": "{{seq}}", "note": "x" }, "expect": { "status": [201] } } ]
            }
            """, runId: null);

        using var request = factory.Create(0);

        Assert.Equal("application/json", request.Content!.Headers.ContentType!.MediaType);
        // Headers are rendered before the body, so they take {{seq}} 1.
        Assert.Equal(["req-1"], request.Headers.GetValues("X-Trace"));
        Assert.Equal("""{"id":2,"note":"x"}""", await request.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["application/json"], request.Headers.GetValues("Accept"));
    }

    [Fact]
    public void ContentTypeHeader_OverridesBodyContentType()
    {
        var factory = CreateFactory("""
            {
              "version": 1, "name": "t", "baseUrl": "http://localhost:5080",
              "load": { "concurrency": 1, "totalRequests": 1 },
              "requests": [ { "name": "form", "method": "POST", "path": "/form",
                "headers": { "Content-Type": "application/x-www-form-urlencoded" },
                "bodyRaw": "a=1", "expect": { "status": [200] } } ]
            }
            """, runId: null);

        using var request = factory.Create(0);

        Assert.Equal(["application/x-www-form-urlencoded"], request.Content!.Headers.GetValues("Content-Type"));
    }

    [Fact]
    public void AuthFalse_MarksRequestToSkipAuth()
    {
        var factory = CreateFactory("""
            {
              "version": 1, "name": "t", "baseUrl": "http://localhost:5080",
              "load": { "concurrency": 1, "totalRequests": 1 },
              "requests": [
                { "name": "with", "method": "GET", "path": "/secure", "expect": { "status": [200] } },
                { "name": "without", "method": "GET", "path": "/secure", "auth": false, "expect": { "status": [401] } } ]
            }
            """, runId: null);

        using var withAuth = factory.Create(0);
        using var withoutAuth = factory.Create(1);

        Assert.False(withAuth.Options.TryGetValue(AuthRequestOptions.SkipAuth, out _));
        Assert.True(withoutAuth.Options.TryGetValue(AuthRequestOptions.SkipAuth, out var skipAuth) && skipAuth);
    }

    private static RequestFactory CreateFactory(string scenarioJson, string? runId)
    {
        var scenario = TestScenarios.Load(scenarioJson).Scenario!;
        var templateContext = new TemplateContext(new FixedTimeProvider(DateTimeOffset.UnixEpoch), new Random(1));
        return new RequestFactory(scenario, templateContext, runId);
    }
}
