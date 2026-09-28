using System.Net;
using LoadKit.Core.Auth;
using LoadKit.Core.Auth.Providers;
using LoadKit.Core.Scenarios.Model;
using LoadKit.Core.Tests.TestSupport;

namespace LoadKit.Core.Tests.Auth;

public sealed class AuthProviderTests
{
    [Fact]
    public async Task Bearer_SetsHeaderUsingFormat_ReplacingExistingValue()
    {
        var provider = new BearerAuthProvider(new BearerAuth("abc-token", "Authorization", "Bearer {token}"));
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/secure");
        request.Headers.TryAddWithoutValidation("Authorization", "old");

        await provider.ApplyAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(["Bearer abc-token"], request.Headers.GetValues("Authorization"));
    }

    [Fact]
    public async Task Bearer_CustomHeaderAndFormat()
    {
        var provider = new BearerAuthProvider(new BearerAuth("abc-token", "X-Token", "{token}"));
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/secure");

        await provider.ApplyAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(["abc-token"], request.Headers.GetValues("X-Token"));
        Assert.False(request.Headers.Contains("Authorization"));
    }

    [Fact]
    public async Task ApiKey_InHeader()
    {
        var provider = new ApiKeyAuthProvider(new ApiKeyAuth("key-123", "x-api-key", null));
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/secure?a=1");

        await provider.ApplyAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(["key-123"], request.Headers.GetValues("x-api-key"));
        Assert.Equal("http://localhost/secure?a=1", request.RequestUri!.AbsoluteUri);
    }

    [Theory]
    [InlineData("http://localhost/secure", "http://localhost/secure?code=k%2By%3D")]
    [InlineData("http://localhost/secure?loadrun=r1", "http://localhost/secure?loadrun=r1&code=k%2By%3D")]
    public async Task ApiKey_InQuery_IsAppendedEscaped(string url, string expectedUrl)
    {
        var provider = new ApiKeyAuthProvider(new ApiKeyAuth("k+y=", null, "code"));
        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        await provider.ApplyAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(expectedUrl, request.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public void ApplyAsync_CompletesSynchronously_WithoutNetwork()
    {
        IAuthProvider[] providers =
        [
            new BearerAuthProvider(new BearerAuth("abc-token", "Authorization", "Bearer {token}")),
            new ApiKeyAuthProvider(new ApiKeyAuth("key-123", null, "code")),
        ];

        foreach (var provider in providers)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/secure");
            Assert.True(provider.ApplyAsync(request, TestContext.Current.CancellationToken).IsCompletedSuccessfully);
        }
    }

    [Fact]
    public async Task Handler_AppliesAuth_SkipsMarkedRequests_AndReportsUnauthorized()
    {
        var provider = new BearerAuthProvider(new BearerAuth("abc-token", "Authorization", "Bearer {token}"));
        var inner = new FakeHttpMessageHandler((request, _) => Task.FromResult(FakeHttpMessageHandler.Response(
            request.Headers.Contains("Authorization") ? HttpStatusCode.OK : HttpStatusCode.Unauthorized)));
        using var client = new HttpClient(new AuthHandler(provider) { InnerHandler = inner });
        var cancellationToken = TestContext.Current.CancellationToken;

        using var authorized = await client.GetAsync(new Uri("http://localhost/secure"), cancellationToken);
        using var skipped = new HttpRequestMessage(HttpMethod.Get, "http://localhost/secure");
        skipped.Options.Set(AuthRequestOptions.SkipAuth, true);
        using var unauthorized = await client.SendAsync(skipped, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Equal(0, provider.StaleSignalCount);
    }

    [Fact]
    public async Task Handler_MarksStale_On401ForAuthenticatedRequest()
    {
        var provider = new ApiKeyAuthProvider(new ApiKeyAuth("revoked-key", "x-api-key", null));
        using var client = new HttpClient(new AuthHandler(provider) { InnerHandler = FakeHttpMessageHandler.Returning(HttpStatusCode.Unauthorized) });

        using var response = await client.GetAsync(new Uri("http://localhost/secure"), TestContext.Current.CancellationToken);

        Assert.Equal(1, provider.StaleSignalCount);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("""{ "type": "bearer", "token": "${env:SECRET}" }""", typeof(BearerAuthProvider))]
    [InlineData("""{ "type": "apiKey", "header": "x-api-key", "value": "${env:SECRET}" }""", typeof(ApiKeyAuthProvider))]
    [InlineData("""{ "type": "login", "request": { "method": "POST", "path": "/auth/login", "body": { "p": "${env:SECRET}" } }, "tokenPath": "$.accessToken" }""", typeof(LoginAuthProvider))]
    [InlineData("""{ "type": "oauth2ClientCredentials", "tokenUrl": "http://localhost:5080/oauth2/token", "clientId": "c", "clientSecret": "${env:SECRET}", "scope": "s" }""", typeof(OAuth2ClientCredentialsAuthProvider))]
    [InlineData("""{ "type": "azureIdentity", "scope": "api://x/.default" }""", typeof(AzureIdentityAuthProvider))]
    public void Factory_CreatesProviderForType(string? auth, Type? expectedType)
    {
        var authField = auth is null ? string.Empty : $""" "auth": {auth},""";
        var scenario = TestScenarios.Load($$"""
            {
              "version": 1, "name": "t", "baseUrl": "http://localhost:5080",{{authField}}
              "load": { "concurrency": 1, "totalRequests": 1 },
              "requests": [ { "name": "r", "method": "GET", "path": "/", "expect": { "status": [200] } } ]
            }
            """, new Dictionary<string, string> { ["SECRET"] = "secret-value" }).Scenario!.Scenario;
        using var tokenHttpClient = new HttpClient(FakeHttpMessageHandler.Returning(HttpStatusCode.OK));
        var factory = new AuthProviderFactory(tokenHttpClient, TimeProvider.System, SecretMasker.CreateEmpty(), _ => new FakeTokenCredential());

        using var provider = factory.Create(scenario);

        Assert.Equal(expectedType, provider?.GetType());
    }
}
