using System.Text.Json;
using System.Text.Json.Nodes;
using LoadKit.Core.Scenarios.Model;

namespace LoadKit.Core.Scenarios.Validation;

/// <summary>Maps a validated scenario tree to model records and applies defaults. Must only see valid input.</summary>
internal static class ScenarioBinder
{
    public static Scenario Bind(JsonObject root)
    {
        return new Scenario(
            Version: RequiredInteger(root, "version"),
            Name: RequiredString(root, "name"),
            Description: OptionalString(root, "description"),
            BaseUrl: RequiredString(root, "baseUrl"),
            Headers: BindStringMap(JsonNodeReader.GetObject(root, "headers"), StringComparer.OrdinalIgnoreCase),
            Auth: BindAuth(JsonNodeReader.GetObject(root, "auth")),
            TagRuns: OptionalBoolean(root, "tagRuns") ?? ScenarioDefaults.TagRuns,
            Load: BindLoad(Required(JsonNodeReader.GetObject(root, "load"), "load")),
            Requests: [.. Required(JsonNodeReader.GetArray(root, "requests"), "requests").Select(node => BindRequest(Required(node as JsonObject, "requests[]")))],
            Thresholds: JsonNodeReader.GetObject(root, "thresholds") is { } thresholds ? BindThresholds(thresholds) : null);
    }

    private static LoadOptions BindLoad(JsonObject load)
    {
        return new LoadOptions(
            Concurrency: RequiredInteger(load, "concurrency"),
            TotalRequests: OptionalInteger(load, "totalRequests"),
            DurationSec: OptionalInteger(load, "durationSec"),
            Warmup: OptionalInteger(load, "warmup") ?? ScenarioDefaults.Warmup,
            TimeoutMs: OptionalInteger(load, "timeoutMs") ?? ScenarioDefaults.TimeoutMs);
    }

    private static RequestDefinition BindRequest(JsonObject request)
    {
        var expect = Required(JsonNodeReader.GetObject(request, "expect"), "expect");
        return new RequestDefinition(
            Name: RequiredString(request, "name"),
            Method: RequiredString(request, "method").ToUpperInvariant(),
            Path: RequiredString(request, "path"),
            Weight: OptionalInteger(request, "weight") ?? ScenarioDefaults.Weight,
            Headers: BindStringMap(JsonNodeReader.GetObject(request, "headers"), StringComparer.OrdinalIgnoreCase),
            Query: BindStringMap(JsonNodeReader.GetObject(request, "query"), StringComparer.Ordinal),
            Body: BindBody(JsonNodeReader.GetObject(request, "body")),
            BodyRaw: OptionalString(request, "bodyRaw"),
            ContentType: OptionalString(request, "contentType") ?? ScenarioDefaults.ContentType,
            AllowEmptyBody: OptionalBoolean(request, "allowEmptyBody") ?? false,
            UseAuth: OptionalBoolean(request, "auth") ?? true,
            Expect: new Expectation(
                [.. Required(JsonNodeReader.GetArray(expect, "status"), "expect.status").Select(node => node!.GetValue<int>())],
                OptionalInteger(expect, "maxMs")));
    }

    private static Thresholds BindThresholds(JsonObject thresholds)
    {
        return new Thresholds(
            P50Ms: OptionalInteger(thresholds, "p50Ms"),
            P95Ms: OptionalInteger(thresholds, "p95Ms"),
            P99Ms: OptionalInteger(thresholds, "p99Ms"),
            ErrorRatePercent: JsonNodeReader.TryGetNumber(thresholds, "errorRatePercent", out var errorRatePercent) ? errorRatePercent : null);
    }

    private static AuthOptions? BindAuth(JsonObject? auth)
    {
        if (auth is null)
        {
            return null;
        }

        var authType = RequiredString(auth, "type");
        return authType switch
        {
            AuthTypeNames.Bearer => new BearerAuth(
                Token: RequiredString(auth, "token"),
                Header: OptionalString(auth, "header") ?? ScenarioDefaults.AuthHeader,
                Format: OptionalString(auth, "format") ?? ScenarioDefaults.AuthFormat),
            AuthTypeNames.ApiKey => new ApiKeyAuth(
                Value: RequiredString(auth, "value"),
                Header: OptionalString(auth, "header"),
                Query: OptionalString(auth, "query")),
            AuthTypeNames.AzureIdentity => new AzureIdentityAuth(
                Scope: RequiredString(auth, "scope"),
                Source: OptionalString(auth, "source") ?? ScenarioDefaults.AzureIdentitySource),
            AuthTypeNames.OAuth2ClientCredentials => new OAuth2ClientCredentialsAuth(
                TokenUrl: RequiredString(auth, "tokenUrl"),
                ClientId: RequiredString(auth, "clientId"),
                ClientSecret: RequiredString(auth, "clientSecret"),
                Scope: RequiredString(auth, "scope")),
            AuthTypeNames.Login => new LoginAuth(
                Request: BindLoginRequest(Required(JsonNodeReader.GetObject(auth, "request"), "auth.request")),
                TokenPath: RequiredString(auth, "tokenPath"),
                ExpiresInPath: OptionalString(auth, "expiresInPath"),
                Header: OptionalString(auth, "header") ?? ScenarioDefaults.AuthHeader,
                Format: OptionalString(auth, "format") ?? ScenarioDefaults.AuthFormat),
            _ => throw new InvalidOperationException($"Unknown auth type '{authType}' passed validation."),
        };
    }

    private static LoginRequestDefinition BindLoginRequest(JsonObject request)
    {
        return new LoginRequestDefinition(
            Method: RequiredString(request, "method").ToUpperInvariant(),
            Path: RequiredString(request, "path"),
            Headers: BindStringMap(JsonNodeReader.GetObject(request, "headers"), StringComparer.OrdinalIgnoreCase),
            Query: BindStringMap(JsonNodeReader.GetObject(request, "query"), StringComparer.Ordinal),
            Body: BindBody(JsonNodeReader.GetObject(request, "body")),
            BodyRaw: OptionalString(request, "bodyRaw"),
            ContentType: OptionalString(request, "contentType") ?? ScenarioDefaults.ContentType);
    }

    private static Dictionary<string, string> BindStringMap(JsonObject? map, StringComparer comparer)
    {
        var result = new Dictionary<string, string>(comparer);
        if (map is null)
        {
            return result;
        }

        foreach (var (name, value) in map)
        {
            result[name] = JsonNodeReader.IsString(value, out var text)
                ? text
                : Required(value, name).ToJsonString();
        }

        return result;
    }

    private static JsonElement? BindBody(JsonObject? body)
    {
        return body is null ? null : JsonSerializer.SerializeToElement(body);
    }

    private static string RequiredString(JsonObject jsonObject, string name)
    {
        return JsonNodeReader.TryGetString(jsonObject, name, out var value) ? value : throw MissingValidated(name);
    }

    private static int RequiredInteger(JsonObject jsonObject, string name)
    {
        return JsonNodeReader.TryGetInteger(jsonObject, name, out var value) ? value : throw MissingValidated(name);
    }

    private static string? OptionalString(JsonObject jsonObject, string name)
    {
        return JsonNodeReader.TryGetString(jsonObject, name, out var value) ? value : null;
    }

    private static int? OptionalInteger(JsonObject jsonObject, string name)
    {
        return JsonNodeReader.TryGetInteger(jsonObject, name, out var value) ? value : null;
    }

    private static bool? OptionalBoolean(JsonObject jsonObject, string name)
    {
        return JsonNodeReader.TryGetBoolean(jsonObject, name, out var value) ? value : null;
    }

    private static T Required<T>(T? value, string name)
        where T : class
    {
        return value ?? throw MissingValidated(name);
    }

    private static InvalidOperationException MissingValidated(string name)
    {
        return new InvalidOperationException($"Validated scenario has no valid '{name}'. The validator and binder disagree.");
    }
}
