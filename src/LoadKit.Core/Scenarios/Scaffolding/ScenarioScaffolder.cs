using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using LoadKit.Core.Scenarios.Model;

namespace LoadKit.Core.Scenarios.Scaffolding;

/// <summary>
/// Generates the starter scenario for <c>loadtest init</c>: safe defaults from the skill (concurrency 5, 200 requests,
/// warmup 20), one placeholder request, and secrets as <c>${env:...}</c> only.
/// </summary>
public static class ScenarioScaffolder
{
    public const string NoAuth = "none";
    public const string DefaultApiKeyHeader = "x-functions-key";

    /// <summary>Values accepted by <c>--auth</c>, in the order they are offered interactively.</summary>
    public static readonly IReadOnlyList<string> AuthChoices =
    [
        NoAuth,
        AuthTypeNames.Bearer,
        AuthTypeNames.ApiKey,
        AuthTypeNames.AzureIdentity,
        AuthTypeNames.OAuth2ClientCredentials,
        AuthTypeNames.Login,
    ];

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static IReadOnlyList<ValidationIssue> Validate(InitOptions options)
    {
        var issues = new List<ValidationIssue>();
        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri) || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            issues.Add(ValidationIssue.Error(ValidationCodes.InvalidValue, "--base-url", $"'{options.BaseUrl}' is not an http(s) URL", "for example --base-url http://localhost:5000"));
        }

        if (!AuthChoices.Contains(options.AuthType))
        {
            issues.Add(ValidationIssue.Error(ValidationCodes.InvalidValue, "--auth", $"unknown auth '{options.AuthType}'", $"use one of: {string.Join(", ", AuthChoices)}"));
        }

        var needsScope = options.AuthType is AuthTypeNames.AzureIdentity or AuthTypeNames.OAuth2ClientCredentials;
        if (needsScope && string.IsNullOrWhiteSpace(options.Scope))
        {
            issues.Add(ValidationIssue.Error(ValidationCodes.RequiredField, "--scope", $"--scope is required for {options.AuthType}", "for your own API: api://<application-id-uri>/.default"));
        }

        if (options.Source is { } source && source != AzureIdentitySources.AzureCli && source != AzureIdentitySources.Default)
        {
            issues.Add(ValidationIssue.Error(ValidationCodes.InvalidValue, "--source", $"unknown source '{source}'", "use azureCli (default) or default"));
        }

        if (!Path.GetExtension(options.ScenarioPath).Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(ValidationIssue.Error(ValidationCodes.InvalidValue, "<file>", "the scenario file must end with .json", "for example loadtests/scenarios/my-api.json"));
        }

        return issues;
    }

    /// <summary>Variables the scenario reads from <c>.env</c>; <c>init</c> adds them there with empty values.</summary>
    public static IReadOnlyList<string> EnvVariables(string authType)
    {
        return authType switch
        {
            AuthTypeNames.Bearer => ["API_TOKEN"],
            AuthTypeNames.ApiKey => ["API_KEY"],
            AuthTypeNames.OAuth2ClientCredentials => ["TOKEN_URL", "CLIENT_ID", "CLIENT_SECRET"],
            AuthTypeNames.Login => ["LOGIN_USER", "LOGIN_PASSWORD"],
            _ => [],
        };
    }

    /// <param name="schemaReference">Relative path from the scenario to <c>scenario.schema.json</c>, with '/'.</param>
    public static string CreateScenarioJson(InitOptions options, string schemaReference)
    {
        var scenario = new JsonObject
        {
            ["$schema"] = schemaReference,
            ["version"] = 1,
            ["name"] = Path.GetFileNameWithoutExtension(options.ScenarioPath),
            ["description"] = "Created by loadtest init: replace the request below with the endpoints you want to load",
            ["baseUrl"] = options.BaseUrl,
        };
        if (CreateAuth(options) is { } auth)
        {
            scenario["auth"] = auth;
        }

        scenario["load"] = new JsonObject { ["concurrency"] = 5, ["totalRequests"] = 200, ["warmup"] = 20 };
        scenario["requests"] = new JsonArray(new JsonObject
        {
            ["name"] = "root",
            ["method"] = "GET",
            ["path"] = "/",
            ["expect"] = new JsonObject { ["status"] = new JsonArray(200) },
        });
        scenario["thresholds"] = new JsonObject { ["p95Ms"] = 500, ["errorRatePercent"] = 1 };
        return scenario.ToJsonString(WriteOptions).ReplaceLineEndings("\n") + "\n";
    }

    private static JsonObject? CreateAuth(InitOptions options)
    {
        return options.AuthType switch
        {
            AuthTypeNames.Bearer => new JsonObject { ["type"] = AuthTypeNames.Bearer, ["token"] = "${env:API_TOKEN}" },
            AuthTypeNames.ApiKey => new JsonObject
            {
                ["type"] = AuthTypeNames.ApiKey,
                ["header"] = options.Header ?? DefaultApiKeyHeader,
                ["value"] = "${env:API_KEY}",
            },
            AuthTypeNames.AzureIdentity => CreateAzureIdentityAuth(options),
            AuthTypeNames.OAuth2ClientCredentials => new JsonObject
            {
                ["type"] = AuthTypeNames.OAuth2ClientCredentials,
                ["tokenUrl"] = "${env:TOKEN_URL}",
                ["clientId"] = "${env:CLIENT_ID}",
                ["clientSecret"] = "${env:CLIENT_SECRET}",
                ["scope"] = options.Scope,
            },
            AuthTypeNames.Login => new JsonObject
            {
                ["type"] = AuthTypeNames.Login,
                ["request"] = new JsonObject
                {
                    ["method"] = "POST",
                    ["path"] = "/auth/login",
                    ["body"] = new JsonObject { ["email"] = "${env:LOGIN_USER}", ["password"] = "${env:LOGIN_PASSWORD}" },
                },
                ["tokenPath"] = "$.accessToken",
            },
            _ => null,
        };
    }

    private static JsonObject CreateAzureIdentityAuth(InitOptions options)
    {
        var auth = new JsonObject { ["type"] = AuthTypeNames.AzureIdentity, ["scope"] = options.Scope };
        if (options.Source == AzureIdentitySources.Default)
        {
            auth["source"] = AzureIdentitySources.Default;
        }

        return auth;
    }
}
