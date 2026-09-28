using System.Text.Json.Nodes;
using LoadKit.Core.Scenarios;
using LoadKit.Core.Tests.TestSupport;

namespace LoadKit.Core.Tests.Scenarios;

public sealed class ScenarioAuthAndEnvValidationTests
{
    private static readonly Dictionary<string, string> Secrets = new()
    {
        ["API_TOKEN"] = "token-value",
        ["FUNC_KEY"] = "key-value",
        ["CLIENT_SECRET"] = "secret-value",
        ["PASSWORD"] = "password-value",
    };

    [Theory]
    [InlineData("""{ "type": "bearer", "token": "${env:API_TOKEN}" }""")]
    [InlineData("""{ "type": "apiKey", "header": "x-functions-key", "value": "${env:FUNC_KEY}" }""")]
    [InlineData("""{ "type": "azureIdentity", "scope": "api://my-api/.default", "source": "default" }""")]
    [InlineData("""{ "type": "oauth2ClientCredentials", "tokenUrl": "https://login.example.com/token", "clientId": "id", "clientSecret": "${env:CLIENT_SECRET}", "scope": "api://x/.default" }""")]
    [InlineData("""{ "type": "login", "request": { "method": "POST", "path": "/auth/login", "body": { "email": "a@b.c", "password": "${env:PASSWORD}" } }, "tokenPath": "$.accessToken" }""")]
    public void ValidAuth_Passes(string authJson)
    {
        var result = LoadWithAuth(authJson);

        Assert.True(result.IsValid, string.Join("; ", result.Issues.Select(issue => issue.Message)));
    }

    [Theory]
    [InlineData("""{ "type": "bearer", "token": "eyJhbGciOi" }""", "auth.token")]
    [InlineData("""{ "type": "apiKey", "header": "x-api-key", "value": "abc123" }""", "auth.value")]
    [InlineData("""{ "type": "oauth2ClientCredentials", "tokenUrl": "https://login.example.com/token", "clientId": "id", "clientSecret": "s3cr3t", "scope": "x" }""", "auth.clientSecret")]
    [InlineData("""{ "type": "login", "request": { "method": "POST", "path": "/login", "body": { "user": { "Password": "hunter2" } } }, "tokenPath": "$.t" }""", "auth.request.body.user.Password")]
    [InlineData("""{ "type": "login", "request": { "method": "POST", "path": "/login", "headers": { "authorization": "Basic abc" } }, "tokenPath": "$.t" }""", "auth.request.headers.authorization")]
    public void SecretLiteral_InAuth_IsError(string authJson, string expectedPath)
    {
        var issue = TestScenarios.SingleIssue(LoadWithAuth(authJson), ValidationCodes.SecretLiteral);

        Assert.Equal(expectedPath, issue.Path);
        Assert.Equal($"secret-like value found in {expectedPath}", issue.Message);
    }

    [Fact]
    public void SecretLiteral_InAuthorizationHeaders_IsError()
    {
        var scenario = TestScenarios.Minimal();
        scenario["headers"] = new JsonObject { ["Authorization"] = "Bearer abc" };
        TestScenarios.FirstRequest(scenario)["headers"] = new JsonObject { ["AUTHORIZATION"] = "Bearer def" };

        var result = TestScenarios.Load(scenario);

        Assert.Equal(
            ["headers.Authorization", "requests[0].headers.AUTHORIZATION"],
            result.Issues.Where(issue => issue.Code == ValidationCodes.SecretLiteral).Select(issue => issue.Path).Order());
    }

    [Fact]
    public void SecretLiteral_IsCheckedBeforeSubstitution_EnvReferenceInsideValueIsAccepted()
    {
        var scenario = TestScenarios.Minimal();
        scenario["headers"] = new JsonObject { ["Authorization"] = "Bearer ${env:API_TOKEN}" };

        var result = TestScenarios.Load(scenario, Secrets);

        Assert.True(result.IsValid);
        Assert.Equal("Bearer token-value", result.Scenario!.Scenario.Headers["authorization"]);
    }

    [Fact]
    public void UnknownAuthType_ListsKnownTypes()
    {
        var issue = TestScenarios.SingleIssue(LoadWithAuth("""{ "type": "basic", "user": "x" }"""), ValidationCodes.InvalidValue);

        Assert.Equal("auth.type", issue.Path);
        Assert.Contains("bearer, apiKey, azureIdentity, oauth2ClientCredentials, login", issue.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingAuthType_IsRequired()
    {
        var issue = TestScenarios.SingleIssue(LoadWithAuth("""{ "token": "${env:API_TOKEN}" }"""), ValidationCodes.RequiredField);

        Assert.Equal("auth.type", issue.Path);
    }

    [Fact]
    public void AuthFieldsAreCheckedPerType()
    {
        var result = LoadWithAuth("""{ "type": "bearer", "scope": "x" }""");

        Assert.Equal("auth.scope", TestScenarios.SingleIssue(result, ValidationCodes.UnknownField).Path);
        Assert.Equal("auth.token", TestScenarios.SingleIssue(result, ValidationCodes.RequiredField).Path);
    }

    [Theory]
    [InlineData("""{ "type": "apiKey", "value": "${env:FUNC_KEY}" }""")]
    [InlineData("""{ "type": "apiKey", "value": "${env:FUNC_KEY}", "header": "h", "query": "q" }""")]
    public void ApiKey_NeedsExactlyOneTarget(string authJson)
    {
        var issue = TestScenarios.SingleIssue(LoadWithAuth(authJson), ValidationCodes.ApiKeyTarget);

        Assert.Equal("auth", issue.Path);
        Assert.Equal("apiKey: specify exactly one of header or query", issue.Message);
    }

    [Fact]
    public void ApiKey_InQuery_IsWarning()
    {
        var result = LoadWithAuth("""{ "type": "apiKey", "query": "code", "value": "${env:FUNC_KEY}" }""");

        var issue = TestScenarios.SingleIssue(result, ValidationCodes.ApiKeyInQuery);
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void AzureIdentity_UnknownSource_IsInvalidValue()
    {
        var issue = TestScenarios.SingleIssue(LoadWithAuth("""{ "type": "azureIdentity", "scope": "x", "source": "vscode" }"""), ValidationCodes.InvalidValue);

        Assert.Equal("auth.source", issue.Path);
    }

    [Fact]
    public void Login_RequestPathAndMethodAreChecked()
    {
        var result = LoadWithAuth("""{ "type": "login", "request": { "method": "SEND", "path": "login" }, "tokenPath": "$.t" }""");

        Assert.Equal(
            ["auth.request.method", "auth.request.path"],
            result.Issues.Where(issue => issue.Code == ValidationCodes.InvalidValue).Select(issue => issue.Path).Order());
    }

    [Fact]
    public void EnvMissing_ReportsVariableOnceWithFirstPath()
    {
        var scenario = TestScenarios.Minimal();
        scenario["baseUrl"] = "${env:BASE_URL}";
        scenario["headers"] = new JsonObject { ["X-A"] = "${env:BASE_URL}", ["X-B"] = "${env:OTHER}" };

        var result = TestScenarios.Load(scenario);

        var issues = result.Issues.Where(issue => issue.Code == ValidationCodes.EnvMissing).ToList();
        Assert.Equal(["baseUrl", "headers.X-B"], issues.Select(issue => issue.Path));
        Assert.Equal("env variable BASE_URL is not set", issues[0].Message);
        Assert.DoesNotContain(result.Issues, issue => issue.Code == ValidationCodes.InvalidValue);
    }

    [Fact]
    public void EnvEmptyValue_CountsAsMissing()
    {
        var scenario = TestScenarios.Minimal();
        scenario["headers"] = new JsonObject { ["X-A"] = "${env:EMPTY}" };

        var result = TestScenarios.Load(scenario, new Dictionary<string, string> { ["EMPTY"] = string.Empty });

        TestScenarios.SingleIssue(result, ValidationCodes.EnvMissing);
    }

    [Fact]
    public void EnvMalformedReference_IsInvalidValue()
    {
        var scenario = TestScenarios.Minimal();
        scenario["headers"] = new JsonObject { ["X-A"] = "${env:1BAD}" };

        var issue = TestScenarios.SingleIssue(TestScenarios.Load(scenario), ValidationCodes.InvalidValue);

        Assert.Equal("headers.X-A", issue.Path);
    }

    [Fact]
    public void EnvSubstitution_IsNotRecursive()
    {
        var scenario = TestScenarios.Minimal();
        scenario["headers"] = new JsonObject { ["X-A"] = "${env:OUTER}" };

        var result = TestScenarios.Load(scenario, new Dictionary<string, string> { ["OUTER"] = "${env:INNER}" });

        Assert.True(result.IsValid);
        Assert.Equal("${env:INNER}", result.Scenario!.Scenario.Headers["X-A"]);
    }

    private static ScenarioLoadResult LoadWithAuth(string authJson)
    {
        var scenario = TestScenarios.Minimal();
        scenario["auth"] = JsonNode.Parse(authJson);
        return TestScenarios.Load(scenario, Secrets);
    }
}
