using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LoadKit.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class TargetApiTests(TargetApiFixture targetApi) : IClassFixture<TargetApiFixture>
{
    private const string DevToken = "dev-token";
    private const string DevApiKey = "dev-api-key";

    [Theory]
    [InlineData("/health")]
    [InlineData("/api/fast")]
    [InlineData("/api/slow?delayMs=1")]
    [InlineData("/api/dep")]
    public async Task Endpoint_ReturnsOk(string path)
    {
        using var httpClient = targetApi.CreateClient();

        using var response = await httpClient.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(0.0, HttpStatusCode.OK)]
    [InlineData(1.0, HttpStatusCode.InternalServerError)]
    public async Task Fail_UsesProbability(double probability, HttpStatusCode expectedStatus)
    {
        using var httpClient = targetApi.CreateClient();

        using var response = await httpClient.GetAsync($"/api/fail?probability={probability}", TestContext.Current.CancellationToken);

        Assert.Equal(expectedStatus, response.StatusCode);
    }

    [Fact]
    public async Task Secure_WithoutCredentials_Returns401()
    {
        using var httpClient = targetApi.CreateClient();

        using var response = await httpClient.GetAsync("/secure", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Secure_WithWrongBearer_Returns401()
    {
        using var httpClient = targetApi.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/secure");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "wrong-token");

        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Secure_WithDevToken_Returns200()
    {
        using var httpClient = targetApi.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/secure");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", DevToken);

        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Secure_WithApiKey_Returns200()
    {
        using var httpClient = targetApi.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/secure");
        request.Headers.Add("x-api-key", DevApiKey);

        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidCredentials_IssuesTokenAcceptedBySecure()
    {
        using var httpClient = targetApi.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        using var loginResponse = await httpClient.PostAsJsonAsync(
            "/auth/login",
            new { email = "loadtest@example.com", password = "dev-password" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        using var loginJson = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync(cancellationToken));
        var accessToken = loginJson.RootElement.GetProperty("accessToken").GetString();
        Assert.Equal(10, loginJson.RootElement.GetProperty("expiresIn").GetInt32());

        var secureStatus = await GetSecureStatusAsync(httpClient, accessToken!, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, secureStatus);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        using var httpClient = targetApi.CreateClient();

        using var response = await httpClient.PostAsJsonAsync(
            "/auth/login",
            new { email = "loadtest@example.com", password = "wrong" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OAuthToken_WithClientCredentials_IssuesTokenAcceptedBySecure()
    {
        using var httpClient = targetApi.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = "loadkit-client",
            ["client_secret"] = "dev-client-secret",
        });

        using var tokenResponse = await httpClient.PostAsync("/oauth2/token", form, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
        using var tokenJson = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(cancellationToken));
        var accessToken = tokenJson.RootElement.GetProperty("access_token").GetString();
        Assert.Equal(10, tokenJson.RootElement.GetProperty("expires_in").GetInt32());

        var secureStatus = await GetSecureStatusAsync(httpClient, accessToken!, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, secureStatus);
    }

    [Fact]
    public async Task OAuthToken_WithWrongSecret_Returns401()
    {
        using var httpClient = targetApi.CreateClient();
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = "loadkit-client",
            ["client_secret"] = "wrong",
        });

        using var response = await httpClient.PostAsync("/oauth2/token", form, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<HttpStatusCode> GetSecureStatusAsync(HttpClient httpClient, string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/secure");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        return response.StatusCode;
    }
}
