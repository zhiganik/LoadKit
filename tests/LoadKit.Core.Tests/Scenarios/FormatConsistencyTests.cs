using System.Reflection;
using System.Text.Json;
using LoadKit.Core.Scenarios.Model;
using LoadKit.Core.Scenarios.Validation;
using LoadKit.Core.Tests.TestSupport;

namespace LoadKit.Core.Tests.Scenarios;

/// <summary>
/// The validator spec (<see cref="ScenarioFormat"/>) is the source of truth; the IDE schema and the model
/// records must describe the same fields.
/// </summary>
[Trait("Category", "Docs")]
public sealed class FormatConsistencyTests
{
    private static readonly Dictionary<string, Type> ModelTypeBySpecName = new(StringComparer.Ordinal)
    {
        ["root"] = typeof(Scenario),
        ["load"] = typeof(LoadOptions),
        ["request"] = typeof(RequestDefinition),
        ["expect"] = typeof(Expectation),
        ["thresholds"] = typeof(Thresholds),
        ["loginRequest"] = typeof(LoginRequestDefinition),
        ["bearerAuth"] = typeof(BearerAuth),
        ["apiKeyAuth"] = typeof(ApiKeyAuth),
        ["azureIdentityAuth"] = typeof(AzureIdentityAuth),
        ["oauth2ClientCredentialsAuth"] = typeof(OAuth2ClientCredentialsAuth),
        ["loginAuth"] = typeof(LoginAuth),
    };

    /// <summary>Scenario field name → model parameter name, where they differ.</summary>
    private static readonly Dictionary<(string SpecName, string FieldName), string?> ModelNameOverrides = new()
    {
        [("root", "$schema")] = null,
        [("request", "auth")] = "useAuth",
    };

    public static TheoryData<string> SpecNames => [.. AllSpecs().Select(spec => spec.Name)];

    [Theory]
    [MemberData(nameof(SpecNames))]
    public void Schema_MatchesSpec(string specName)
    {
        var spec = AllSpecs().Single(candidate => candidate.Name == specName);
        using var schema = JsonDocument.Parse(File.ReadAllText(RepositoryPaths.Combine("schemas", "scenario.schema.json")));
        var definition = specName == "root" ? schema.RootElement : schema.RootElement.GetProperty("$defs").GetProperty(specName);

        var schemaProperties = definition.GetProperty("properties").EnumerateObject().Select(property => property.Name).Order();
        IEnumerable<string> schemaRequired = definition.TryGetProperty("required", out var required)
            ? required.EnumerateArray().Select(item => item.GetString()!).Order()
            : [];

        Assert.Equal(spec.Fields.Select(field => field.Name).Order(), schemaProperties);
        Assert.Equal(spec.Fields.Where(field => field.IsRequired).Select(field => field.Name).Order(), schemaRequired);
        Assert.False(definition.GetProperty("additionalProperties").GetBoolean());
    }

    [Fact]
    public void Schema_AuthTypes_MatchSpec()
    {
        using var schema = JsonDocument.Parse(File.ReadAllText(RepositoryPaths.Combine("schemas", "scenario.schema.json")));
        var auth = schema.RootElement.GetProperty("$defs").GetProperty("auth");

        var schemaTypes = auth.GetProperty("properties").GetProperty("type").GetProperty("enum").EnumerateArray().Select(item => item.GetString());
        var schemaVariants = auth.GetProperty("oneOf").EnumerateArray().Select(item => item.GetProperty("$ref").GetString());

        Assert.Equal(ScenarioFormat.AuthTypes.Keys, schemaTypes);
        Assert.Equal(ScenarioFormat.AuthTypes.Values.Select(spec => "#/$defs/" + spec.Name), schemaVariants);
        foreach (var (typeName, spec) in ScenarioFormat.AuthTypes)
        {
            var typeConst = schema.RootElement.GetProperty("$defs").GetProperty(spec.Name).GetProperty("properties").GetProperty("type").GetProperty("const");
            Assert.Equal(typeName, typeConst.GetString());
        }
    }

    [Theory]
    [MemberData(nameof(SpecNames))]
    public void Model_MatchesSpec(string specName)
    {
        var spec = AllSpecs().Single(candidate => candidate.Name == specName);
        var modelType = ModelTypeBySpecName[specName];
        var isAuth = typeof(AuthOptions).IsAssignableFrom(modelType);

        var expectedParameters = spec.Fields
            .Where(field => !(isAuth && field.Name == "type"))
            .Select(field => ModelNameOverrides.TryGetValue((specName, field.Name), out var overridden) ? overridden : field.Name)
            .OfType<string>()
            .Select(name => name.ToLowerInvariant())
            .Order();
        var modelParameters = modelType
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Single()
            .GetParameters()
            .Select(parameter => parameter.Name!.ToLowerInvariant())
            .Order();

        Assert.Equal(expectedParameters, modelParameters);
    }

    [Fact]
    public void EverySpec_HasModelType()
    {
        Assert.Equal(ModelTypeBySpecName.Keys.Order(), AllSpecs().Select(spec => spec.Name).Order());
    }

    private static IEnumerable<ObjectSpec> AllSpecs()
    {
        return
        [
            ScenarioFormat.Root,
            ScenarioFormat.Load,
            ScenarioFormat.Request,
            ScenarioFormat.Expect,
            ScenarioFormat.Thresholds,
            ScenarioFormat.LoginRequest,
            .. ScenarioFormat.AuthTypes.Values,
        ];
    }
}
