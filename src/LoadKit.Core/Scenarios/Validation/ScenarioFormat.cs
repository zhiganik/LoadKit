using LoadKit.Core.Scenarios.Model;

namespace LoadKit.Core.Scenarios.Validation;

/// <summary>
/// The scenario format as data: allowed fields, their kinds and required flags. Drives structural validation;
/// a test checks it against <c>schemas/scenario.schema.json</c> and the model records.
/// </summary>
internal static class ScenarioFormat
{
    public const int CurrentVersion = 1;

    public static readonly IReadOnlyList<string> HttpMethods = ["GET", "POST", "PUT", "PATCH", "DELETE"];

    public static readonly IReadOnlyList<string> AzureIdentitySourceNames = [AzureIdentitySources.AzureCli, AzureIdentitySources.Default];

    private const string ExpectMissingHint = "list the expected status codes, for example \"expect\": { \"status\": [200] }";

    public static readonly ObjectSpec Expect = new("expect",
    [
        new("status", FieldKind.IntegerArray, IsRequired: true, MissingCode: ValidationCodes.ExpectMissing, MissingHint: ExpectMissingHint),
        new("maxMs", FieldKind.Integer),
    ]);

    public static readonly ObjectSpec Request = new("request",
    [
        new("name", FieldKind.String, IsRequired: true),
        new("method", FieldKind.String, IsRequired: true),
        new("path", FieldKind.String, IsRequired: true),
        new("weight", FieldKind.Integer),
        new("headers", FieldKind.StringMap),
        new("query", FieldKind.ScalarMap),
        new("body", FieldKind.JsonObject),
        new("bodyRaw", FieldKind.String),
        new("contentType", FieldKind.String),
        new("allowEmptyBody", FieldKind.Boolean),
        new("auth", FieldKind.Boolean),
        new("expect", FieldKind.Object, IsRequired: true, Object: Expect, MissingCode: ValidationCodes.ExpectMissing, MissingHint: ExpectMissingHint),
    ]);

    public static readonly ObjectSpec Load = new("load",
    [
        new("concurrency", FieldKind.Integer, IsRequired: true),
        new("totalRequests", FieldKind.Integer),
        new("durationSec", FieldKind.Integer),
        new("warmup", FieldKind.Integer),
        new("timeoutMs", FieldKind.Integer),
    ]);

    public static readonly ObjectSpec Thresholds = new("thresholds",
    [
        new("p50Ms", FieldKind.Integer),
        new("p95Ms", FieldKind.Integer),
        new("p99Ms", FieldKind.Integer),
        new("errorRatePercent", FieldKind.Number),
    ]);

    public static readonly ObjectSpec LoginRequest = new("loginRequest",
    [
        new("method", FieldKind.String, IsRequired: true),
        new("path", FieldKind.String, IsRequired: true),
        new("headers", FieldKind.StringMap),
        new("query", FieldKind.ScalarMap),
        new("body", FieldKind.JsonObject),
        new("bodyRaw", FieldKind.String),
        new("contentType", FieldKind.String),
    ]);

    /// <summary>Auth variants by <c>auth.type</c>, in the order shown to users.</summary>
    public static readonly IReadOnlyDictionary<string, ObjectSpec> AuthTypes = new Dictionary<string, ObjectSpec>(StringComparer.Ordinal)
    {
        [AuthTypeNames.Bearer] = new("bearerAuth",
        [
            AuthType(),
            new("token", FieldKind.String, IsRequired: true),
            new("header", FieldKind.String),
            new("format", FieldKind.String),
        ]),
        [AuthTypeNames.ApiKey] = new("apiKeyAuth",
        [
            AuthType(),
            new("value", FieldKind.String, IsRequired: true),
            new("header", FieldKind.String),
            new("query", FieldKind.String),
        ]),
        [AuthTypeNames.AzureIdentity] = new("azureIdentityAuth",
        [
            AuthType(),
            new("scope", FieldKind.String, IsRequired: true),
            new("source", FieldKind.String),
        ]),
        [AuthTypeNames.OAuth2ClientCredentials] = new("oauth2ClientCredentialsAuth",
        [
            AuthType(),
            new("tokenUrl", FieldKind.String, IsRequired: true),
            new("clientId", FieldKind.String, IsRequired: true),
            new("clientSecret", FieldKind.String, IsRequired: true),
            new("scope", FieldKind.String, IsRequired: true),
        ]),
        [AuthTypeNames.Login] = new("loginAuth",
        [
            AuthType(),
            new("request", FieldKind.Object, IsRequired: true, Object: LoginRequest),
            new("tokenPath", FieldKind.String, IsRequired: true),
            new("expiresInPath", FieldKind.String),
            new("header", FieldKind.String),
            new("format", FieldKind.String),
        ]),
    };

    public static readonly ObjectSpec Root = new("root",
    [
        new("$schema", FieldKind.String),
        new("version", FieldKind.Integer, IsRequired: true, MissingHint: "add \"version\": 1"),
        new("name", FieldKind.String, IsRequired: true),
        new("description", FieldKind.String),
        new("baseUrl", FieldKind.String, IsRequired: true, MissingHint: "for example \"baseUrl\": \"http://localhost:5080\""),
        new("headers", FieldKind.StringMap),
        new("auth", FieldKind.Auth),
        new("tagRuns", FieldKind.Boolean),
        new("load", FieldKind.Object, IsRequired: true, Object: Load, MissingHint: "for example \"load\": { \"concurrency\": 5, \"totalRequests\": 200 }"),
        new("requests", FieldKind.ObjectArray, IsRequired: true, Object: Request),
        new("thresholds", FieldKind.Object, Object: Thresholds),
    ]);

    public static string AuthTypeList => string.Join(", ", AuthTypes.Keys);

    private static FieldSpec AuthType()
    {
        return new FieldSpec("type", FieldKind.String, IsRequired: true);
    }
}
