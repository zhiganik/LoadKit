using System.Text.Json;
using LoadKit.Core.Scenarios;
using LoadKit.Core.Tests.TestSupport;

namespace LoadKit.Core.Tests.Scenarios;

public sealed class JsonPathQueryTests
{
    private const string Response = """
        { "accessToken": "a", "data": { "token": "b", "items": [ { "id": 1 }, { "id": 2 } ] }, "access-token": "c" }
        """;

    [Theory]
    [InlineData("$.accessToken", "\"a\"")]
    [InlineData("$.data.token", "\"b\"")]
    [InlineData("$.data.items[1].id", "2")]
    [InlineData("$['access-token']", "\"c\"")]
    [InlineData("$.data['items'][0]", """{ "id": 1 }""")]
    public void TryRead_FindsValue(string path, string expectedJson)
    {
        using var document = JsonDocument.Parse(Response);

        Assert.True(JsonPathQuery.Parse(path).TryRead(document.RootElement, out var value));
        Assert.Equal(expectedJson, value.GetRawText());
    }

    [Theory]
    [InlineData("$.missing")]
    [InlineData("$.data.items[5]")]
    [InlineData("$.accessToken.inner")]
    [InlineData("$[0]")]
    public void TryRead_ReturnsFalseWhenAbsent(string path)
    {
        using var document = JsonDocument.Parse(Response);

        Assert.False(JsonPathQuery.Parse(path).TryRead(document.RootElement, out _));
    }

    [Theory]
    [InlineData("accessToken")]
    [InlineData("$.")]
    [InlineData("$.a[")]
    [InlineData("$.a[x]")]
    [InlineData("$..a")]
    [InlineData("$a")]
    public void TryParse_RejectsUnsupportedSyntax(string path)
    {
        Assert.False(JsonPathQuery.TryParse(path, out _, out var error));
        Assert.NotEmpty(error);
    }

    [Theory]
    [InlineData("tokenPath")]
    [InlineData("expiresInPath")]
    public void Validator_ReportsInvalidLoginPaths(string fieldName)
    {
        var scenario = TestScenarios.Minimal();
        var auth = System.Text.Json.Nodes.JsonNode.Parse("""
            { "type": "login", "request": { "method": "POST", "path": "/auth/login", "body": { "n": 1 } },
              "tokenPath": "$.accessToken" }
            """)!.AsObject();
        auth[fieldName] = "accessToken";
        scenario["auth"] = auth;

        var issue = TestScenarios.SingleIssue(TestScenarios.Load(scenario), ValidationCodes.InvalidValue);

        Assert.Equal($"auth.{fieldName}", issue.Path);
        Assert.Contains("must start with '$'", issue.Message, StringComparison.Ordinal);
    }
}
