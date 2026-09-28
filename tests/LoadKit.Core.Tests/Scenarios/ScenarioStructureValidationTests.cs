using System.Text.Json.Nodes;
using LoadKit.Core.Scenarios;
using LoadKit.Core.Tests.TestSupport;

namespace LoadKit.Core.Tests.Scenarios;

public sealed class ScenarioStructureValidationTests
{
    [Fact]
    public void MinimalScenario_IsValidWithoutIssues()
    {
        var result = TestScenarios.Load(TestScenarios.Minimal());

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void InvalidJson_IsReportedAtRoot()
    {
        var result = TestScenarios.Load("{ \"version\": 1, ");

        var issue = TestScenarios.SingleIssue(result, ValidationCodes.InvalidJson);
        Assert.Equal("$", issue.Path);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void DuplicateProperty_IsInvalidJson()
    {
        var result = TestScenarios.Load("""{ "version": 1, "version": 1 }""");

        TestScenarios.SingleIssue(result, ValidationCodes.InvalidJson);
    }

    [Fact]
    public void RootArray_IsInvalidType()
    {
        var issue = TestScenarios.SingleIssue(TestScenarios.Load("[]"), ValidationCodes.InvalidType);

        Assert.Equal("$", issue.Path);
    }

    [Fact]
    public void CommentsTrailingCommasAndBom_AreAllowed()
    {
        var json = "﻿" + """
            {
              // comment
              "version": 1,
              "name": "test",
              "baseUrl": "http://localhost:5080",
              "load": { "concurrency": 1, "totalRequests": 1, },
              "requests": [
                { "name": "a", "method": "GET", "path": "/", "expect": { "status": [200] } },
              ],
            }
            """;

        Assert.True(TestScenarios.Load(json).IsValid);
    }

    [Fact]
    public void UnsupportedVersion_StopsValidation()
    {
        var scenario = TestScenarios.Minimal();
        scenario["version"] = 2;
        scenario["unknown"] = true;

        var result = TestScenarios.Load(scenario);

        var issue = Assert.Single(result.Issues);
        Assert.Equal(ValidationCodes.UnsupportedVersion, issue.Code);
        Assert.Equal("version", issue.Path);
    }

    [Fact]
    public void SchemaField_IsAllowed()
    {
        var scenario = TestScenarios.Minimal();
        scenario["$schema"] = "../scenario.schema.json";

        Assert.True(TestScenarios.Load(scenario).IsValid);
    }

    [Theory]
    [InlineData("retries", "the field is not in the scenario format")]
    [InlineData("baseURL", "did you mean 'baseUrl'?")]
    [InlineData("threshold", "did you mean 'thresholds'?")]
    public void UnknownRootField_IsReportedWithHint(string fieldName, string expectedHintStart)
    {
        var scenario = TestScenarios.Minimal();
        scenario[fieldName] = 1;

        var issue = TestScenarios.SingleIssue(TestScenarios.Load(scenario), ValidationCodes.UnknownField);

        Assert.Equal(fieldName, issue.Path);
        Assert.Equal($"unknown field '{fieldName}'", issue.Message);
        Assert.StartsWith(expectedHintStart, issue.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownNestedField_HasFullPath()
    {
        var scenario = TestScenarios.Minimal();
        scenario["load"]!["retries"] = 3;
        TestScenarios.FirstRequest(scenario)["expect"]!["statusCode"] = 200;

        var result = TestScenarios.Load(scenario);

        Assert.Equal(
            ["load.retries", "requests[0].expect.statusCode"],
            result.Issues.Where(issue => issue.Code == ValidationCodes.UnknownField).Select(issue => issue.Path).Order());
    }

    [Theory]
    [InlineData("version")]
    [InlineData("name")]
    [InlineData("baseUrl")]
    [InlineData("load")]
    [InlineData("requests")]
    public void MissingRequiredRootField_IsReported(string fieldName)
    {
        var scenario = TestScenarios.Minimal();
        scenario.Remove(fieldName);

        var issue = TestScenarios.SingleIssue(TestScenarios.Load(scenario), ValidationCodes.RequiredField);

        Assert.Equal(fieldName, issue.Path);
        Assert.Equal($"{fieldName} is required", issue.Message);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("method")]
    [InlineData("path")]
    public void MissingRequiredRequestField_IsReported(string fieldName)
    {
        var scenario = TestScenarios.Minimal();
        TestScenarios.FirstRequest(scenario).Remove(fieldName);

        var issue = TestScenarios.SingleIssue(TestScenarios.Load(scenario), ValidationCodes.RequiredField);

        Assert.Equal($"requests[0].{fieldName}", issue.Path);
    }

    [Fact]
    public void MissingExpect_IsExpectMissing()
    {
        var scenario = TestScenarios.Minimal();
        TestScenarios.FirstRequest(scenario).Remove("expect");

        var issue = TestScenarios.SingleIssue(TestScenarios.Load(scenario), ValidationCodes.ExpectMissing);

        Assert.Equal("requests[0].expect", issue.Path);
        Assert.NotNull(issue.Hint);
    }

    [Fact]
    public void MissingExpectStatus_IsExpectMissing()
    {
        var scenario = TestScenarios.Minimal();
        TestScenarios.FirstRequest(scenario)["expect"] = new JsonObject { ["maxMs"] = 100 };

        var issue = TestScenarios.SingleIssue(TestScenarios.Load(scenario), ValidationCodes.ExpectMissing);

        Assert.Equal("requests[0].expect.status", issue.Path);
    }

    [Fact]
    public void EmptyExpectStatus_IsExpectMissing()
    {
        var scenario = TestScenarios.Minimal();
        TestScenarios.FirstRequest(scenario)["expect"]!["status"] = new JsonArray();

        var issue = TestScenarios.SingleIssue(TestScenarios.Load(scenario), ValidationCodes.ExpectMissing);

        Assert.Equal("requests[0].expect.status", issue.Path);
    }

    [Theory]
    [InlineData("name", "123", "name must be a string")]
    [InlineData("tagRuns", "\"yes\"", "tagRuns must be true or false")]
    [InlineData("headers", "[]", "headers must be an object with string values")]
    [InlineData("load", "5", "load must be an object")]
    [InlineData("requests", "{}", "requests must be an array of objects")]
    [InlineData("description", "null", "description must be a string")]
    public void WrongRootType_IsReported(string fieldName, string jsonValue, string expectedMessage)
    {
        var scenario = TestScenarios.Minimal();
        scenario[fieldName] = JsonNode.Parse(jsonValue);

        var issue = TestScenarios.SingleIssue(TestScenarios.Load(scenario), ValidationCodes.InvalidType);

        Assert.Equal(fieldName, issue.Path);
        Assert.Equal(expectedMessage, issue.Message);
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("\"5\"")]
    [InlineData("3000000000")]
    public void NonIntegerConcurrency_IsInvalidType(string jsonValue)
    {
        var scenario = TestScenarios.Minimal();
        scenario["load"]!["concurrency"] = JsonNode.Parse(jsonValue);

        var issue = TestScenarios.SingleIssue(TestScenarios.Load(scenario), ValidationCodes.InvalidType);

        Assert.Equal("load.concurrency", issue.Path);
    }

    [Fact]
    public void HeaderValueMustBeString_QueryValueMayBeScalar()
    {
        var scenario = TestScenarios.Minimal();
        var request = TestScenarios.FirstRequest(scenario);
        request["headers"] = new JsonObject { ["X-Count"] = 1 };
        request["query"] = new JsonObject { ["page"] = 1, ["active"] = true, ["q"] = "x", ["bad"] = new JsonArray() };

        var result = TestScenarios.Load(scenario);

        Assert.Equal(
            ["requests[0].headers.X-Count", "requests[0].query.bad"],
            result.Issues.Where(issue => issue.Code == ValidationCodes.InvalidType).Select(issue => issue.Path).Order());
    }

    [Fact]
    public void ExpectStatusItems_MustBeIntegers()
    {
        var scenario = TestScenarios.Minimal();
        TestScenarios.FirstRequest(scenario)["expect"]!["status"] = new JsonArray(200, "201");

        var issue = TestScenarios.SingleIssue(TestScenarios.Load(scenario), ValidationCodes.InvalidType);

        Assert.Equal("requests[0].expect.status[1]", issue.Path);
    }

    [Fact]
    public void HeaderNameWithSpace_UsesBracketPath()
    {
        var scenario = TestScenarios.Minimal();
        scenario["headers"] = new JsonObject { ["X Custom"] = 5 };

        var issue = TestScenarios.SingleIssue(TestScenarios.Load(scenario), ValidationCodes.InvalidType);

        Assert.Equal("headers['X Custom']", issue.Path);
    }

    [Fact]
    public void ThreeDifferentErrors_AreAllReported()
    {
        var scenario = TestScenarios.Minimal();
        scenario["load"]!["retries"] = 3;
        TestScenarios.FirstRequest(scenario)["method"] = "POST";
        TestScenarios.FirstRequest(scenario)["path"] = "/orders/{{uuid}}";

        var result = TestScenarios.Load(scenario);

        Assert.False(result.IsValid);
        Assert.Equal(
            [ValidationCodes.BodyRequired, ValidationCodes.UnknownField, ValidationCodes.UnknownTemplate],
            result.Issues.Select(issue => issue.Code).Order());
        Assert.All(result.Issues, issue => Assert.False(string.IsNullOrEmpty(issue.Hint)));
    }
}
