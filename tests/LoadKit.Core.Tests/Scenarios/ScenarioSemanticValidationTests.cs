using System.Text.Json.Nodes;
using LoadKit.Core.Scenarios;
using LoadKit.Core.Tests.TestSupport;

namespace LoadKit.Core.Tests.Scenarios;

public sealed class ScenarioSemanticValidationTests
{
    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    public void BodyRequired_ForMethodsWithBody(string method)
    {
        var scenario = WithFirstRequest(request => request["method"] = method);

        var issue = TestScenarios.SingleIssue(TestScenarios.Load(scenario), ValidationCodes.BodyRequired);

        Assert.Equal("requests[0].body", issue.Path);
        Assert.Equal($"requests[0].body is required for {method}", issue.Message);
        Assert.Equal("add \"body\" or \"allowEmptyBody\": true", issue.Hint);
    }

    [Theory]
    [InlineData("body", "{}")]
    [InlineData("bodyRaw", "\"text\"")]
    [InlineData("allowEmptyBody", "true")]
    public void BodyRequired_IsSatisfied(string fieldName, string jsonValue)
    {
        var scenario = WithFirstRequest(request =>
        {
            request["method"] = "post";
            request[fieldName] = JsonNode.Parse(jsonValue);
        });

        Assert.True(TestScenarios.Load(scenario).IsValid);
    }

    [Fact]
    public void BodyOnGet_IsWarningOnly()
    {
        var scenario = WithFirstRequest(request => request["body"] = new JsonObject { ["a"] = 1 });

        var result = TestScenarios.Load(scenario);

        var issue = TestScenarios.SingleIssue(result, ValidationCodes.BodyOnGet);
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Equal("requests[0].body", issue.Path);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void BodyAndBodyRaw_Conflict()
    {
        var scenario = WithFirstRequest(request =>
        {
            request["method"] = "POST";
            request["body"] = new JsonObject();
            request["bodyRaw"] = "x";
        });

        var issue = TestScenarios.SingleIssue(TestScenarios.Load(scenario), ValidationCodes.BodyConflict);

        Assert.Equal("requests[0]", issue.Path);
    }

    [Fact]
    public void UnsupportedMethod_IsInvalidValue()
    {
        var scenario = WithFirstRequest(request => request["method"] = "HEAD");

        var issue = TestScenarios.SingleIssue(TestScenarios.Load(scenario), ValidationCodes.InvalidValue);

        Assert.Equal("requests[0].method", issue.Path);
        Assert.Contains("GET, POST, PUT, PATCH, DELETE", issue.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void PathWithoutLeadingSlash_IsInvalidValue()
    {
        var scenario = WithFirstRequest(request => request["path"] = "api/orders");

        var issue = TestScenarios.SingleIssue(TestScenarios.Load(scenario), ValidationCodes.InvalidValue);

        Assert.Equal("requests[0].path", issue.Path);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void LoadMode_RequiresExactlyOneOfTotalAndDuration(bool hasTotalRequests, bool hasDuration)
    {
        var scenario = TestScenarios.Minimal();
        var load = scenario["load"]!.AsObject();
        load.Remove("totalRequests");
        if (hasTotalRequests)
        {
            load["totalRequests"] = 10;
        }

        if (hasDuration)
        {
            load["durationSec"] = 10;
        }

        Assert.True(TestScenarios.Load(scenario).IsValid);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LoadMode_BothOrNeither_IsError(bool both)
    {
        var scenario = TestScenarios.Minimal();
        var load = scenario["load"]!.AsObject();
        if (both)
        {
            load["durationSec"] = 10;
        }
        else
        {
            load.Remove("totalRequests");
        }

        var issue = TestScenarios.SingleIssue(TestScenarios.Load(scenario), ValidationCodes.LoadMode);

        Assert.Equal("load", issue.Path);
        Assert.Equal("load: specify either totalRequests or durationSec", issue.Message);
    }

    [Theory]
    [InlineData("concurrency", 0)]
    [InlineData("totalRequests", 0)]
    [InlineData("warmup", -1)]
    [InlineData("timeoutMs", 0)]
    public void LoadValuesBelowMinimum_AreInvalid(string fieldName, int value)
    {
        var scenario = TestScenarios.Minimal();
        scenario["load"]![fieldName] = value;

        var issue = TestScenarios.SingleIssue(TestScenarios.Load(scenario), ValidationCodes.InvalidValue);

        Assert.Equal($"load.{fieldName}", issue.Path);
    }

    [Fact]
    public void ConcurrencyGreaterThanTotal_IsWarning()
    {
        var scenario = TestScenarios.Minimal();
        scenario["load"]!["concurrency"] = 20;

        var result = TestScenarios.Load(scenario);

        var issue = TestScenarios.SingleIssue(result, ValidationCodes.ConcurrencyGreaterThanTotal);
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Equal("load.concurrency", issue.Path);
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(10, true)]
    [InlineData(9, false)]
    public void WarmupGreaterOrEqualTotal_IsError(int warmup, bool isError)
    {
        var scenario = TestScenarios.Minimal();
        scenario["load"]!["warmup"] = warmup;

        var result = TestScenarios.Load(scenario);

        Assert.Equal(isError, result.Issues.Any(issue => issue.Code == ValidationCodes.WarmupGreaterThanTotal));
        Assert.Equal(!isError, result.IsValid);
    }

    [Fact]
    public void EmptyRequests_IsInvalidValue()
    {
        var scenario = TestScenarios.Minimal();
        scenario["requests"] = new JsonArray();

        var issue = TestScenarios.SingleIssue(TestScenarios.Load(scenario), ValidationCodes.InvalidValue);

        Assert.Equal("requests", issue.Path);
    }

    [Fact]
    public void DuplicateRequestName_PointsToSecondRequest()
    {
        var scenario = TestScenarios.Minimal();
        var requests = scenario["requests"]!.AsArray();
        requests.Add(TestScenarios.FirstRequest(scenario).DeepClone());

        var issue = TestScenarios.SingleIssue(TestScenarios.Load(scenario), ValidationCodes.DuplicateRequestName);

        Assert.Equal("requests[1].name", issue.Path);
        Assert.Equal("request name 'health' is already used by requests[0]", issue.Message);
    }

    [Fact]
    public void WeightBelowOne_IsInvalidValue()
    {
        var scenario = WithFirstRequest(request => request["weight"] = 0);

        var issue = TestScenarios.SingleIssue(TestScenarios.Load(scenario), ValidationCodes.InvalidValue);

        Assert.Equal("requests[0].weight", issue.Path);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(600)]
    public void StatusOutsideHttpRange_IsInvalidValue(int status)
    {
        var scenario = WithFirstRequest(request => request["expect"]!["status"] = new JsonArray(200, status));

        var issue = TestScenarios.SingleIssue(TestScenarios.Load(scenario), ValidationCodes.InvalidValue);

        Assert.Equal("requests[0].expect.status[1]", issue.Path);
    }

    [Theory]
    [InlineData("path", "/orders/{{uuid}}", "requests[0].path")]
    [InlineData("bodyRaw", "id={{guid:1}}", "requests[0].bodyRaw")]
    public void TemplateErrors_InRequestStrings(string fieldName, string value, string expectedPath)
    {
        var scenario = WithFirstRequest(request => request[fieldName] = value);

        var issue = Assert.Single(
            TestScenarios.Load(scenario).Issues,
            issue => issue.Path == expectedPath && issue.Severity == ValidationSeverity.Error);

        Assert.Contains(issue.Code, new[] { ValidationCodes.UnknownTemplate, ValidationCodes.InvalidTemplate });
    }

    [Fact]
    public void TemplateErrors_InHeadersQueryAndNestedBody()
    {
        var scenario = TestScenarios.Minimal();
        scenario["headers"] = new JsonObject { ["X-Root"] = "{{nope}}" };
        var request = TestScenarios.FirstRequest(scenario);
        request["method"] = "POST";
        request["headers"] = new JsonObject { ["X-Id"] = "{{uuid}}" };
        request["query"] = new JsonObject { ["page"] = "{{randomInt:5:1}}" };
        request["body"] = new JsonObject { ["items"] = new JsonArray(new JsonObject { ["id"] = "{{uid}}" }) };

        var result = TestScenarios.Load(scenario);

        Assert.Equal(
            ["headers.X-Root", "requests[0].body.items[0].id", "requests[0].headers.X-Id", "requests[0].query.page"],
            result.Issues.Select(issue => issue.Path).Order());
    }

    [Theory]
    [InlineData("ftp://localhost")]
    [InlineData("localhost:5080")]
    [InlineData("/relative")]
    public void BaseUrlNotAbsoluteHttp_IsInvalidValue(string baseUrl)
    {
        var scenario = TestScenarios.Minimal();
        scenario["baseUrl"] = baseUrl;

        var issue = TestScenarios.SingleIssue(TestScenarios.Load(scenario), ValidationCodes.InvalidValue);

        Assert.Equal("baseUrl", issue.Path);
    }

    [Theory]
    [InlineData("https://api.example.com", true)]
    [InlineData("http://localhost:5080", false)]
    [InlineData("http://127.0.0.1:5080", false)]
    [InlineData("http://[::1]:5080", false)]
    public void RemoteBaseUrl_IsInfo(string baseUrl, bool isRemote)
    {
        var scenario = TestScenarios.Minimal();
        scenario["baseUrl"] = baseUrl;

        var result = TestScenarios.Load(scenario);

        Assert.True(result.IsValid);
        var remoteIssues = result.Issues.Where(issue => issue.Code == ValidationCodes.RemoteUrl).ToList();
        Assert.Equal(isRemote ? 1 : 0, remoteIssues.Count);
        Assert.All(remoteIssues, issue => Assert.Equal(ValidationSeverity.Info, issue.Severity));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ThresholdMsBelowOne_IsInvalidValue(int value)
    {
        var scenario = TestScenarios.Minimal();
        scenario["thresholds"] = new JsonObject { ["p95Ms"] = value };

        var issue = TestScenarios.SingleIssue(TestScenarios.Load(scenario), ValidationCodes.InvalidValue);

        Assert.Equal("thresholds.p95Ms", issue.Path);
    }

    [Theory]
    [InlineData(-0.1, false)]
    [InlineData(0, true)]
    [InlineData(100, true)]
    [InlineData(100.5, false)]
    public void ErrorRatePercent_MustBePercentage(double value, bool isValid)
    {
        var scenario = TestScenarios.Minimal();
        scenario["thresholds"] = new JsonObject { ["errorRatePercent"] = value };

        Assert.Equal(isValid, TestScenarios.Load(scenario).IsValid);
    }

    private static JsonObject WithFirstRequest(Action<JsonObject> change)
    {
        var scenario = TestScenarios.Minimal();
        change(TestScenarios.FirstRequest(scenario));
        return scenario;
    }
}
