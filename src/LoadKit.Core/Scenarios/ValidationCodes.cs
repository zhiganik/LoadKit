namespace LoadKit.Core.Scenarios;

/// <summary>Stable validation codes. They are shown to users and AI agents; do not rename.</summary>
public static class ValidationCodes
{
    // Input
    public const string FileNotFound = "file-not-found";
    public const string EnvFileNotFound = "env-file-not-found";
    public const string InvalidJson = "invalid-json";
    public const string UnsupportedVersion = "unsupported-version";

    // Structure
    public const string UnknownField = "unknown-field";
    public const string InvalidType = "invalid-type";
    public const string RequiredField = "required-field";
    public const string InvalidValue = "invalid-value";

    // Semantic rules
    public const string BodyRequired = "body-required";
    public const string BodyOnGet = "body-on-get";
    public const string BodyConflict = "body-conflict";
    public const string LoadMode = "load-mode";
    public const string ConcurrencyGreaterThanTotal = "concurrency-gt-total";
    public const string WarmupGreaterThanTotal = "warmup-gt-total";
    public const string UnknownTemplate = "unknown-template";
    public const string InvalidTemplate = "invalid-template";
    public const string EnvMissing = "env-missing";
    public const string SecretLiteral = "secret-literal";
    public const string DuplicateRequestName = "duplicate-request-name";
    public const string ExpectMissing = "expect-missing";
    public const string RemoteUrl = "remote-url";
    public const string ApiKeyTarget = "apikey-target";
    public const string ApiKeyInQuery = "apikey-in-query";
}
