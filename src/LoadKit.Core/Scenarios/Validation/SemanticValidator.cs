using System.Text.Json.Nodes;
using LoadKit.Core.Scenarios.Model;
using LoadKit.Core.Scenarios.Templates;

namespace LoadKit.Core.Scenarios.Validation;

/// <summary>
/// Rules across fields and values (see docs/architecture/SCENARIOS.md). Runs after env substitution and
/// tolerates structural errors: a rule is skipped when the fields it needs are missing or have the wrong type.
/// </summary>
internal sealed class SemanticValidator(ICollection<ValidationIssue> issues, TemplateCompiler templateCompiler)
{
    private static readonly HashSet<string> MethodsRequiringBody = new(StringComparer.Ordinal) { "POST", "PUT", "PATCH" };
    private static readonly HashSet<string> MethodsWithoutBody = new(StringComparer.Ordinal) { "GET", "DELETE" };

    public void Validate(JsonObject root)
    {
        ValidateBaseUrl(root);
        ValidateTemplatesInMap(JsonNodeReader.GetObject(root, "headers"), "headers");
        ValidateLoad(JsonNodeReader.GetObject(root, "load"));
        ValidateRequests(JsonNodeReader.GetArray(root, "requests"));
        ValidateThresholds(JsonNodeReader.GetObject(root, "thresholds"));
        ValidateAuth(JsonNodeReader.GetObject(root, "auth"));
    }

    private void ValidateBaseUrl(JsonObject root)
    {
        if (!JsonNodeReader.TryGetString(root, "baseUrl", out var baseUrl))
        {
            return;
        }

        var uri = ValidateAbsoluteHttpUrl(baseUrl, "baseUrl");
        if (uri is not null && !LocalAddress.IsLocal(uri))
        {
            issues.Add(ValidationIssue.Info(
                ValidationCodes.RemoteUrl,
                "baseUrl",
                $"baseUrl {uri.GetLeftPart(UriPartial.Authority)} is not localhost",
                "loadtest check and run ask for confirmation; without a terminal pass --yes only after the user agreed"));
        }
    }

    private void ValidateLoad(JsonObject? load)
    {
        if (load is null)
        {
            return;
        }

        var hasTotalRequests = load.ContainsKey("totalRequests");
        var hasDuration = load.ContainsKey("durationSec");
        if (hasTotalRequests == hasDuration)
        {
            issues.Add(ValidationIssue.Error(
                ValidationCodes.LoadMode,
                "load",
                "load: specify either totalRequests or durationSec",
                hasTotalRequests
                    ? "keep only one of the two fields"
                    : "add \"totalRequests\" (number of requests) or \"durationSec\" (run length in seconds)"));
        }

        RequireAtLeast(load, "concurrency", 1, "load.concurrency");
        RequireAtLeast(load, "totalRequests", 1, "load.totalRequests");
        RequireAtLeast(load, "durationSec", 1, "load.durationSec");
        RequireAtLeast(load, "warmup", 0, "load.warmup");
        RequireAtLeast(load, "timeoutMs", 1, "load.timeoutMs");

        if (!JsonNodeReader.TryGetInteger(load, "totalRequests", out var totalRequests) || totalRequests < 1)
        {
            return;
        }

        if (JsonNodeReader.TryGetInteger(load, "concurrency", out var concurrency) && concurrency > totalRequests)
        {
            issues.Add(ValidationIssue.Warning(
                ValidationCodes.ConcurrencyGreaterThanTotal,
                "load.concurrency",
                $"load.concurrency ({concurrency}) is greater than load.totalRequests ({totalRequests})",
                "only totalRequests workers will ever be busy; lower concurrency or raise totalRequests"));
        }

        if (JsonNodeReader.TryGetInteger(load, "warmup", out var warmup) && warmup >= totalRequests)
        {
            issues.Add(ValidationIssue.Error(
                ValidationCodes.WarmupGreaterThanTotal,
                "load.warmup",
                $"load.warmup ({warmup}) must be less than load.totalRequests ({totalRequests})",
                "warmup requests are not measured; lower warmup or raise totalRequests"));
        }
    }

    private void ValidateRequests(JsonArray? requests)
    {
        if (requests is null)
        {
            return;
        }

        if (requests.Count == 0)
        {
            issues.Add(ValidationIssue.Error(
                ValidationCodes.InvalidValue,
                "requests",
                "requests must contain at least one request",
                "for example { \"name\": \"health\", \"method\": \"GET\", \"path\": \"/health\", \"expect\": { \"status\": [200] } }"));
            return;
        }

        var firstIndexByName = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < requests.Count; index++)
        {
            if (requests[index] is not JsonObject request)
            {
                continue;
            }

            var requestPath = JsonPath.Index("requests", index);
            if (JsonNodeReader.TryGetString(request, "name", out var name) && !firstIndexByName.TryAdd(name, index))
            {
                issues.Add(ValidationIssue.Error(
                    ValidationCodes.DuplicateRequestName,
                    requestPath + ".name",
                    $"request name '{name}' is already used by requests[{firstIndexByName[name]}]",
                    "names must be unique; they label rows in the report"));
            }

            ValidateRequest(request, requestPath);
        }
    }

    private void ValidateRequest(JsonObject request, string requestPath)
    {
        var method = ValidateMethod(request, requestPath);
        if (ValidatePath(request, requestPath) is { } path)
        {
            ValidateTemplate(path, requestPath + ".path");
        }

        RequireAtLeast(request, "weight", 1, requestPath + ".weight");
        ValidateBody(request, requestPath, method, isLoginRequest: false);
        ValidateTemplatesInMap(JsonNodeReader.GetObject(request, "headers"), requestPath + ".headers");
        ValidateTemplatesInMap(JsonNodeReader.GetObject(request, "query"), requestPath + ".query");
        ValidateTemplatesInJson(JsonNodeReader.GetObject(request, "body"), requestPath + ".body");
        if (JsonNodeReader.TryGetString(request, "bodyRaw", out var bodyRaw))
        {
            ValidateTemplate(bodyRaw, requestPath + ".bodyRaw");
        }

        ValidateExpect(JsonNodeReader.GetObject(request, "expect"), requestPath + ".expect");
    }

    private string? ValidateMethod(JsonObject request, string requestPath)
    {
        if (!JsonNodeReader.TryGetString(request, "method", out var method))
        {
            return null;
        }

        var normalizedMethod = method.ToUpperInvariant();
        if (ScenarioFormat.HttpMethods.Contains(normalizedMethod))
        {
            return normalizedMethod;
        }

        issues.Add(ValidationIssue.Error(
            ValidationCodes.InvalidValue,
            requestPath + ".method",
            $"{requestPath}.method '{method}' is not supported",
            "use one of: " + string.Join(", ", ScenarioFormat.HttpMethods)));
        return null;
    }

    private string? ValidatePath(JsonObject request, string requestPath)
    {
        if (!JsonNodeReader.TryGetString(request, "path", out var path))
        {
            return null;
        }

        if (!path.StartsWith('/'))
        {
            issues.Add(ValidationIssue.Error(
                ValidationCodes.InvalidValue,
                requestPath + ".path",
                $"{requestPath}.path must start with '/'",
                "paths are relative to baseUrl, for example \"/api/orders\""));
        }

        return path;
    }

    private void ValidateBody(JsonObject request, string requestPath, string? method, bool isLoginRequest)
    {
        var hasBody = request.ContainsKey("body");
        var hasBodyRaw = request.ContainsKey("bodyRaw");
        if (hasBody && hasBodyRaw)
        {
            issues.Add(ValidationIssue.Error(
                ValidationCodes.BodyConflict,
                requestPath,
                $"{requestPath}: use either body or bodyRaw, not both",
                "body is a JSON object; bodyRaw is a string sent as-is with contentType"));
        }

        if (method is null || isLoginRequest)
        {
            return;
        }

        JsonNodeReader.TryGetBoolean(request, "allowEmptyBody", out var allowEmptyBody);
        if (MethodsRequiringBody.Contains(method) && !hasBody && !hasBodyRaw && !allowEmptyBody)
        {
            issues.Add(ValidationIssue.Error(
                ValidationCodes.BodyRequired,
                requestPath + ".body",
                $"{requestPath}.body is required for {method}",
                "add \"body\" or \"allowEmptyBody\": true"));
        }

        if (MethodsWithoutBody.Contains(method) && (hasBody || hasBodyRaw))
        {
            var bodyPath = requestPath + (hasBody ? ".body" : ".bodyRaw");
            issues.Add(ValidationIssue.Warning(
                ValidationCodes.BodyOnGet,
                bodyPath,
                $"{bodyPath} is sent with {method}",
                "most servers ignore a body on GET and DELETE; remove it unless the API expects one"));
        }
    }

    private void ValidateExpect(JsonObject? expect, string expectPath)
    {
        if (expect is null)
        {
            return;
        }

        if (JsonNodeReader.GetArray(expect, "status") is { } statuses)
        {
            if (statuses.Count == 0)
            {
                issues.Add(ValidationIssue.Error(
                    ValidationCodes.ExpectMissing,
                    expectPath + ".status",
                    $"{expectPath}.status must list at least one status code",
                    "for example \"status\": [200]"));
            }

            for (var index = 0; index < statuses.Count; index++)
            {
                if (JsonNodeReader.IsInteger(statuses[index], out var statusCode) && statusCode is < 100 or > 599)
                {
                    var statusPath = JsonPath.Index(expectPath + ".status", index);
                    issues.Add(ValidationIssue.Error(
                        ValidationCodes.InvalidValue,
                        statusPath,
                        $"{statusPath}: {statusCode} is not an HTTP status code",
                        "use codes from 100 to 599"));
                }
            }
        }

        RequireAtLeast(expect, "maxMs", 1, expectPath + ".maxMs");
    }

    private void ValidateThresholds(JsonObject? thresholds)
    {
        if (thresholds is null)
        {
            return;
        }

        RequireAtLeast(thresholds, "p50Ms", 1, "thresholds.p50Ms");
        RequireAtLeast(thresholds, "p95Ms", 1, "thresholds.p95Ms");
        RequireAtLeast(thresholds, "p99Ms", 1, "thresholds.p99Ms");
        if (JsonNodeReader.TryGetNumber(thresholds, "errorRatePercent", out var errorRatePercent) && errorRatePercent is < 0 or > 100)
        {
            issues.Add(ValidationIssue.Error(
                ValidationCodes.InvalidValue,
                "thresholds.errorRatePercent",
                "thresholds.errorRatePercent must be between 0 and 100",
                "it is a percentage: 1 means at most 1% of requests may fail"));
        }
    }

    private void ValidateAuth(JsonObject? auth)
    {
        if (!JsonNodeReader.TryGetString(auth, "type", out var authType))
        {
            return;
        }

        switch (authType)
        {
            case AuthTypeNames.ApiKey:
                ValidateApiKeyTarget(auth!);
                break;
            case AuthTypeNames.AzureIdentity:
                if (JsonNodeReader.TryGetString(auth, "source", out var source) && !ScenarioFormat.AzureIdentitySourceNames.Contains(source))
                {
                    issues.Add(ValidationIssue.Error(
                        ValidationCodes.InvalidValue,
                        "auth.source",
                        $"unknown azureIdentity source '{source}'",
                        "use \"azureCli\" (default, uses az login) or \"default\" (DefaultAzureCredential)"));
                }

                break;
            case AuthTypeNames.OAuth2ClientCredentials:
                if (JsonNodeReader.TryGetString(auth, "tokenUrl", out var tokenUrl))
                {
                    ValidateAbsoluteHttpUrl(tokenUrl, "auth.tokenUrl");
                }

                break;
            case AuthTypeNames.Login:
                if (JsonNodeReader.GetObject(auth, "request") is { } loginRequest)
                {
                    var method = ValidateMethod(loginRequest, "auth.request");
                    ValidatePath(loginRequest, "auth.request");
                    ValidateBody(loginRequest, "auth.request", method, isLoginRequest: true);
                }

                ValidateJsonPath(auth!, "tokenPath");
                ValidateJsonPath(auth!, "expiresInPath");
                break;
        }
    }

    private void ValidateApiKeyTarget(JsonObject auth)
    {
        var hasHeader = auth.ContainsKey("header");
        var hasQuery = auth.ContainsKey("query");
        if (hasHeader == hasQuery)
        {
            issues.Add(ValidationIssue.Error(
                ValidationCodes.ApiKeyTarget,
                "auth",
                "apiKey: specify exactly one of header or query",
                "use \"header\": \"x-functions-key\" (recommended) or \"query\": \"code\""));
        }
        else if (hasQuery)
        {
            issues.Add(ValidationIssue.Warning(
                ValidationCodes.ApiKeyInQuery,
                "auth.query",
                "apiKey is sent in the query string",
                "the key ends up in URLs and server logs; prefer \"header\" when the API supports it"));
        }
    }

    private void ValidateJsonPath(JsonObject auth, string fieldName)
    {
        if (JsonNodeReader.TryGetString(auth, fieldName, out var jsonPath) && !JsonPathQuery.TryParse(jsonPath, out _, out var error))
        {
            issues.Add(ValidationIssue.Error(
                ValidationCodes.InvalidValue,
                $"auth.{fieldName}",
                $"auth.{fieldName} '{jsonPath}' is not a supported JSONPath: {error}",
                "use $.name, $.a.b, $.items[0] or $['name-with-dash']"));
        }
    }

    private Uri? ValidateAbsoluteHttpUrl(string value, string path)
    {
        // An unresolved ${env:} reference is already reported as env-missing.
        if (EnvReferences.ContainsReference(value))
        {
            return null;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return uri;
        }

        issues.Add(ValidationIssue.Error(
            ValidationCodes.InvalidValue,
            path,
            $"{path} must be an absolute http or https URL",
            "for example \"http://localhost:5080\""));
        return null;
    }

    private void RequireAtLeast(JsonObject jsonObject, string name, int minimum, string path)
    {
        if (JsonNodeReader.TryGetInteger(jsonObject, name, out var value) && value < minimum)
        {
            issues.Add(ValidationIssue.Error(ValidationCodes.InvalidValue, path, $"{path} must be at least {minimum}"));
        }
    }

    private void ValidateTemplatesInMap(JsonObject? map, string mapPath)
    {
        if (map is null)
        {
            return;
        }

        foreach (var (name, value) in map)
        {
            if (JsonNodeReader.IsString(value, out var text))
            {
                ValidateTemplate(text, JsonPath.Child(mapPath, name));
            }
        }
    }

    private void ValidateTemplatesInJson(JsonNode? node, string path)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                foreach (var (name, value) in jsonObject)
                {
                    ValidateTemplatesInJson(value, JsonPath.Child(path, name));
                }

                break;
            case JsonArray jsonArray:
                for (var index = 0; index < jsonArray.Count; index++)
                {
                    ValidateTemplatesInJson(jsonArray[index], JsonPath.Index(path, index));
                }

                break;
            default:
                if (JsonNodeReader.IsString(node, out var text))
                {
                    ValidateTemplate(text, path);
                }

                break;
        }
    }

    private void ValidateTemplate(string text, string path)
    {
        foreach (var error in templateCompiler.Compile(text).Errors)
        {
            issues.Add(ValidationIssue.Error(error.Code, path, error.Message, error.Hint));
        }
    }
}
