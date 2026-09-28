using System.Net;
using System.Text;
using Azure.Core;
using Azure.Identity;
using LoadKit.Core.Auth;
using LoadKit.Core.Auth.Providers;
using LoadKit.Core.Scenarios.Model;
using LoadKit.Core.Tests.TestSupport;
using Microsoft.Extensions.Time.Testing;

namespace LoadKit.Core.Tests.Auth;

/// <summary>login, oauth2ClientCredentials and azureIdentity against fake token sources.</summary>
public sealed class TokenProviderTests
{
    private const string LoginPassword = "login-password-value";
    private const string ClientSecret = "client-secret-value";

    private readonly FakeTimeProvider _timeProvider = new();
    private readonly SecretMasker _secretMasker = SecretMasker.CreateEmpty();

    [Fact]
    public async Task Login_SendsRequest_ReadsTokenAndExpiresIn()
    {
        HttpRequestMessage? sentRequest = null;
        string? sentBody = null;
        var handler = new FakeHttpMessageHandler(async (request, cancellationToken) =>
        {
            sentRequest = request;
            sentBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return Json(HttpStatusCode.OK, """{ "data": { "accessToken": "login-token-1" }, "expiresIn": 10 }""");
        });
        using var provider = CreateLoginProvider(handler, tokenPath: "$.data.accessToken", expiresInPath: "$.expiresIn");

        await provider.InitializeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Post, sentRequest!.Method);
        Assert.Equal("http://localhost:5080/auth/login?tenant=dev", sentRequest.RequestUri!.AbsoluteUri);
        Assert.Equal($$"""{ "email": "user@example.com", "password": "{{LoginPassword}}" }""", sentBody);
        Assert.Equal("application/json", sentRequest.Content!.Headers.ContentType!.MediaType);
        Assert.Equal(_timeProvider.GetUtcNow().AddSeconds(10), provider.ExpiresAt);
        Assert.Equal("X-Auth: ***", _secretMasker.MaskText("X-Auth: login-token-1"));
    }

    [Fact]
    public async Task Login_WithoutExpiresInPath_UsesJwtExp()
    {
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(2_000_000_000);
        var jwt = CreateJwt($$"""{"sub":"u","exp":{{expiresAt.ToUnixTimeSeconds()}}}""");
        var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, $$"""{ "accessToken": "{{jwt}}" }""")));
        using var provider = CreateLoginProvider(handler, tokenPath: "$.accessToken", expiresInPath: null);

        await provider.InitializeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expiresAt, provider.ExpiresAt);
    }

    [Fact]
    public async Task Login_OpaqueTokenWithoutExpiry_HasUnknownLifetime()
    {
        var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, """{ "accessToken": "opaque-token" }""")));
        using var provider = CreateLoginProvider(handler, tokenPath: "$.accessToken", expiresInPath: null);

        await provider.InitializeAsync(TestContext.Current.CancellationToken);

        Assert.Null(provider.ExpiresAt);
    }

    [Fact]
    public async Task Login_Rejected_ThrowsWithStatusAndHint()
    {
        var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(Json(HttpStatusCode.Unauthorized, """{"error":"bad credentials"}""")));
        using var provider = CreateLoginProvider(handler, tokenPath: "$.accessToken", expiresInPath: null);

        var exception = await Assert.ThrowsAsync<AuthException>(() => provider.InitializeAsync(TestContext.Current.CancellationToken));

        Assert.Contains("POST /auth/login returned 401", exception.Message, StringComparison.Ordinal);
        Assert.Contains(".env", exception.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_TokenPathNotFound_NamesPropertiesButNotValues()
    {
        var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, """{ "token": "real-token-value", "ttl": 60 }""")));
        using var provider = CreateLoginProvider(handler, tokenPath: "$.accessToken", expiresInPath: null);

        var exception = await Assert.ThrowsAsync<AuthException>(() => provider.InitializeAsync(TestContext.Current.CancellationToken));

        Assert.Contains("$.accessToken", exception.Message, StringComparison.Ordinal);
        Assert.Equal("the response has properties: token, ttl; fix auth.tokenPath", exception.Hint);
        Assert.DoesNotContain("real-token-value", exception.Message + exception.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_ConnectionFailure_ThrowsWithHint()
    {
        var handler = new FakeHttpMessageHandler((_, _) => throw new HttpRequestException(HttpRequestError.ConnectionError, "refused"));
        using var provider = CreateLoginProvider(handler, tokenPath: "$.accessToken", expiresInPath: null);

        var exception = await Assert.ThrowsAsync<AuthException>(() => provider.InitializeAsync(TestContext.Current.CancellationToken));

        Assert.Contains("API is running", exception.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OAuth2_PostsClientCredentialsForm_AndReadsExpiresIn()
    {
        string? sentForm = null;
        var handler = new FakeHttpMessageHandler(async (request, cancellationToken) =>
        {
            sentForm = await request.Content!.ReadAsStringAsync(cancellationToken);
            return Json(HttpStatusCode.OK, """{ "access_token": "oauth-token-1", "token_type": "Bearer", "expires_in": 3600 }""");
        });
        using var provider = CreateOAuth2Provider(handler);

        await provider.InitializeAsync(TestContext.Current.CancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/");
        await provider.ApplyAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal($"grant_type=client_credentials&client_id=loadkit-client&client_secret={ClientSecret}&scope=api%3A%2F%2Ftarget%2F.default", sentForm);
        Assert.Equal(["Bearer oauth-token-1"], request.Headers.GetValues("Authorization"));
        Assert.Equal(_timeProvider.GetUtcNow().AddSeconds(3600), provider.ExpiresAt);
    }

    [Fact]
    public async Task OAuth2_InvalidClient_ExplainsWithoutSecret()
    {
        var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(Json(
            HttpStatusCode.Unauthorized, """{ "error": "invalid_client", "error_description": "Client authentication failed." }""")));
        using var provider = CreateOAuth2Provider(handler);

        var exception = await Assert.ThrowsAsync<AuthException>(() => provider.InitializeAsync(TestContext.Current.CancellationToken));

        Assert.Contains("returned 401: invalid_client: Client authentication failed.", exception.Message, StringComparison.Ordinal);
        Assert.Equal("check auth.clientId and auth.clientSecret in .env", exception.Hint);
        Assert.DoesNotContain(ClientSecret, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AzureIdentity_GetsTokenForScope()
    {
        var expiresOn = _timeProvider.GetUtcNow().AddMinutes(60);
        var credential = new FakeTokenCredential(_ => new AccessToken("azure-token-1", expiresOn));
        using var provider = new AzureIdentityAuthProvider(new AzureIdentityAuth("api://target/.default", "azureCli"), credential, _timeProvider, _secretMasker);

        await provider.InitializeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["api://target/.default"], Assert.Single(credential.RequestedScopes));
        Assert.Equal(expiresOn, provider.ExpiresAt);
    }

    [Fact]
    public async Task AzureIdentity_NotLoggedIn_HintsAzLogin()
    {
        var credential = new FakeTokenCredential(_ => throw new CredentialUnavailableException("Please run 'az login' to set up account.\nMore details"));
        using var provider = new AzureIdentityAuthProvider(new AzureIdentityAuth("api://target/.default", "azureCli"), credential, _timeProvider, _secretMasker);

        var exception = await Assert.ThrowsAsync<AuthException>(() => provider.InitializeAsync(TestContext.Current.CancellationToken));

        Assert.Contains("'az login'", exception.Hint, StringComparison.Ordinal);
        Assert.DoesNotContain("More details", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AzureIdentity_ConsentError_HintsAuthorizedClientApplication()
    {
        var credential = new FakeTokenCredential(_ => throw new AuthenticationFailedException(
            "AADSTS65001: The user or administrator has not consented to use the application 'Microsoft Azure CLI'."));
        using var provider = new AzureIdentityAuthProvider(new AzureIdentityAuth("api://target/.default", "azureCli"), credential, _timeProvider, _secretMasker);

        var exception = await Assert.ThrowsAsync<AuthException>(() => provider.InitializeAsync(TestContext.Current.CancellationToken));

        Assert.Contains("AADSTS65001", exception.Message, StringComparison.Ordinal);
        Assert.Contains(AzureIdentityAuthProvider.AzureCliClientId, exception.Hint, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not-a-jwt")]
    [InlineData("a.b.c")]
    [InlineData("eyJhbGciOiJub25lIn0.eyJzdWIiOiJ1In0.")]
    public void JwtExpiry_IsNullForTokensWithoutExp(string token)
    {
        Assert.Null(JwtExpiry.TryRead(token));
    }

    private LoginAuthProvider CreateLoginProvider(HttpMessageHandler handler, string tokenPath, string? expiresInPath)
    {
        using var body = System.Text.Json.JsonDocument.Parse($$"""{ "email": "user@example.com", "password": "{{LoginPassword}}" }""");
        var request = new LoginRequestDefinition(
            "POST",
            "/auth/login",
            new Dictionary<string, string>(),
            new Dictionary<string, string> { ["tenant"] = "dev" },
            body.RootElement.Clone(),
            null,
            "application/json");
        var options = new LoginAuth(request, tokenPath, expiresInPath, "X-Auth", "{token}");
        return new LoginAuthProvider(options, "http://localhost:5080/", new HttpClient(handler), _timeProvider, _secretMasker);
    }

    private OAuth2ClientCredentialsAuthProvider CreateOAuth2Provider(HttpMessageHandler handler)
    {
        var options = new OAuth2ClientCredentialsAuth("http://localhost:5080/oauth2/token", "loadkit-client", ClientSecret, "api://target/.default");
        return new OAuth2ClientCredentialsAuthProvider(options, new HttpClient(handler), _timeProvider, _secretMasker);
    }

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string json)
    {
        return new HttpResponseMessage(statusCode) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    private static string CreateJwt(string payloadJson)
    {
        static string Encode(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{Encode("""{"alg":"none"}""")}.{Encode(payloadJson)}.signature";
    }
}
