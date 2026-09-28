using LoadKit.Core.Auth;
using LoadKit.Core.Tests.TestSupport;

namespace LoadKit.Core.Tests.Auth;

public sealed class SecretMaskerTests
{
    [Fact]
    public void MaskText_HidesSecretsAndTheirUrlEncodedForm()
    {
        var masker = new SecretMasker(["p@ss word+1"], []);

        var masked = masker.MaskText("body p@ss word+1, url ?code=p%40ss%20word%2B1&x=1");

        Assert.Equal("body ***, url ?code=***&x=1", masked);
    }

    [Fact]
    public void AddSecret_MasksValuesAcquiredAtRuntime()
    {
        var masker = SecretMasker.CreateEmpty();

        masker.AddSecret("runtime-token");

        Assert.Equal("Bearer ***", masker.MaskText("Bearer runtime-token"));
        Assert.Equal("Bearer runtime-token", SecretMasker.CreateEmpty().MaskText("Bearer runtime-token"));
    }

    [Fact]
    public void MaskText_IgnoresValuesShorterThanMinimum()
    {
        var masker = new SecretMasker(["abc"], []);

        Assert.Equal("abc", masker.MaskText("abc"));
    }

    [Theory]
    [InlineData("Authorization")]
    [InlineData("x-functions-key")]
    [InlineData("COOKIE")]
    [InlineData("api-key")]
    [InlineData("X-API-KEY")]
    [InlineData("X-Custom-Token")]
    public void SensitiveHeaders_AreMaskedEntirely(string headerName)
    {
        var masker = new SecretMasker([], ["x-custom-token"]);

        Assert.Equal(SecretMasker.Mask, masker.MaskHeaderValue(headerName, "Bearer anything"));
    }

    [Fact]
    public void OtherHeaders_AreMaskedOnlyForKnownSecrets()
    {
        var masker = new SecretMasker(["secret-value"], []);

        Assert.Equal("application/json", masker.MaskHeaderValue("Accept", "application/json"));
        Assert.Equal("x ***", masker.MaskHeaderValue("X-Trace", "x secret-value"));
    }

    [Fact]
    public void FromScenario_CollectsAuthSecretsAndSensitiveHeaderValues()
    {
        var scenario = TestScenarios.Load("""
            {
              "version": 1, "name": "t", "baseUrl": "http://localhost:5080",
              "headers": { "x-functions-key": "${env:FUNC_KEY}" },
              "auth": { "type": "bearer", "token": "${env:API_TOKEN}", "header": "X-Token" },
              "load": { "concurrency": 1, "totalRequests": 1 },
              "requests": [ { "name": "r", "method": "GET", "path": "/", "expect": { "status": [200] } } ]
            }
            """,
            new Dictionary<string, string> { ["API_TOKEN"] = "token-from-env", ["FUNC_KEY"] = "function-key-value" }).Scenario!.Scenario;

        var masker = SecretMasker.FromScenario(scenario);

        Assert.Equal("*** and ***", masker.MaskText("token-from-env and function-key-value"));
        Assert.True(masker.IsSensitiveHeader("X-Token"));
    }

    [Fact]
    public void FromScenario_MasksLoginBodyValues()
    {
        var scenario = TestScenarios.Load("""
            {
              "version": 1, "name": "t", "baseUrl": "http://localhost:5080",
              "auth": {
                "type": "login",
                "request": { "method": "POST", "path": "/auth/login", "body": { "email": "${env:USER}", "password": "${env:PASSWORD}" } },
                "tokenPath": "$.accessToken"
              },
              "load": { "concurrency": 1, "totalRequests": 1 },
              "requests": [ { "name": "r", "method": "GET", "path": "/", "expect": { "status": [200] } } ]
            }
            """,
            new Dictionary<string, string> { ["USER"] = "user@example.com", ["PASSWORD"] = "hunter2-password" }).Scenario!.Scenario;

        var masker = SecretMasker.FromScenario(scenario);

        Assert.Equal("login *** / *** failed", masker.MaskText("login user@example.com / hunter2-password failed"));
    }
}
