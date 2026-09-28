using System.Text.Json;
using LoadKit.Core.Metrics;
using LoadKit.Core.Reporting;
using LoadKit.Core.Tests.TestSupport;

namespace LoadKit.Core.Tests.Reporting;

/// <summary>
/// report.json is a public contract: the serialized report must match schemas/report.schema.json exactly
/// (every property declared, every declared property present, types and enums respected), and the schema must not
/// declare properties the report never writes.
/// </summary>
[Trait("Category", "Docs")]
public sealed class ReportSchemaConsistencyTests
{
    private static readonly JsonElement Schema = JsonDocument.Parse(File.ReadAllText(RepositoryPaths.Combine("schemas", "report.schema.json"))).RootElement;

    [Fact]
    public void FullReport_MatchesSchema()
    {
        using var report = JsonDocument.Parse(JsonReportWriter.Write(ReportWriterTests.SampleReport()));
        var errors = new List<string>();

        SchemaChecker.Check(Schema, Schema, report.RootElement, "$", errors);

        Assert.Empty(errors);
    }

    [Fact]
    public void MinimalReport_MatchesSchema()
    {
        var minimal = ReportWriterTests.SampleReport() with
        {
            Scenario = new ReportScenario("t", null, "http://localhost", null),
            Requests = [],
            Histogram = [],
            Thresholds = [],
            ThresholdsPassed = null,
            ErrorSamples = [],
            Warnings = [],
            ApplicationInsights = null,
        };
        using var report = JsonDocument.Parse(JsonReportWriter.Write(minimal));
        var errors = new List<string>();

        SchemaChecker.Check(Schema, Schema, report.RootElement, "$", errors);

        Assert.Empty(errors);
    }

    [Fact]
    public void SchemaVersion_MatchesModel()
    {
        Assert.Equal(RunReport.CurrentSchemaVersion, Schema.GetProperty("properties").GetProperty("schemaVersion").GetProperty("const").GetInt32());
    }

    [Fact]
    public void WarningCodes_MatchSchemaEnum()
    {
        var schemaCodes = Schema.GetProperty("$defs").GetProperty("warning").GetProperty("properties").GetProperty("code").GetProperty("enum")
            .EnumerateArray().Select(code => code.GetString()).Order();
        var modelCodes = typeof(ReportWarningCodes).GetFields().Select(field => (string?)field.GetValue(null)).Order();

        Assert.Equal(modelCodes, schemaCodes);
    }

    [Fact]
    public void ErrorKinds_MatchSchemaEnum()
    {
        var schemaKinds = Schema.GetProperty("$defs").GetProperty("errorKind").GetProperty("properties").GetProperty("kind").GetProperty("enum")
            .EnumerateArray().Select(kind => kind.GetString()).Order();
        var modelKinds = Enum.GetNames<ErrorKind>().Where(name => name != nameof(ErrorKind.None)).Order();

        Assert.Equal(modelKinds, schemaKinds);
    }

    /// <summary>
    /// The subset of JSON Schema used by report.schema.json: type (with null), const, enum, properties, required,
    /// additionalProperties: false, items, $ref to #/$defs, oneOf. Own code: JSON Schema libraries are excluded by license.
    /// </summary>
    private static class SchemaChecker
    {
        public static void Check(JsonElement root, JsonElement schema, JsonElement value, string path, List<string> errors)
        {
            if (schema.TryGetProperty("$ref", out var reference))
            {
                var name = reference.GetString()!["#/$defs/".Length..];
                Check(root, root.GetProperty("$defs").GetProperty(name), value, path, errors);
                return;
            }

            if (schema.TryGetProperty("oneOf", out var oneOf))
            {
                var matches = oneOf.EnumerateArray().Count(option =>
                {
                    var optionErrors = new List<string>();
                    Check(root, option, value, path, optionErrors);
                    return optionErrors.Count == 0;
                });
                if (matches != 1)
                {
                    errors.Add($"{path}: matches {matches} oneOf options");
                }

                return;
            }

            if (schema.TryGetProperty("type", out var type) && !MatchesType(type, value))
            {
                errors.Add($"{path}: {value.ValueKind} does not match type {type}");
                return;
            }

            if (schema.TryGetProperty("const", out var constant) && value.GetRawText() != constant.GetRawText())
            {
                errors.Add($"{path}: expected {constant}");
            }

            if (schema.TryGetProperty("enum", out var allowed) && !allowed.EnumerateArray().Any(option => option.GetRawText() == value.GetRawText()))
            {
                errors.Add($"{path}: {value} is not in enum");
            }

            if (value.ValueKind == JsonValueKind.Object)
            {
                CheckObject(root, schema, value, path, errors);
            }
            else if (value.ValueKind == JsonValueKind.Array && schema.TryGetProperty("items", out var items))
            {
                var index = 0;
                foreach (var item in value.EnumerateArray())
                {
                    Check(root, items, item, $"{path}[{index++}]", errors);
                }
            }
        }

        private static void CheckObject(JsonElement root, JsonElement schema, JsonElement value, string path, List<string> errors)
        {
            var properties = schema.GetProperty("properties");
            foreach (var property in value.EnumerateObject())
            {
                if (properties.TryGetProperty(property.Name, out var propertySchema))
                {
                    Check(root, propertySchema, property.Value, $"{path}.{property.Name}", errors);
                }
                else
                {
                    errors.Add($"{path}.{property.Name}: not declared in the schema");
                }
            }

            foreach (var required in schema.GetProperty("required").EnumerateArray())
            {
                if (!value.TryGetProperty(required.GetString()!, out _))
                {
                    errors.Add($"{path}.{required.GetString()}: required but missing");
                }
            }

            foreach (var declared in properties.EnumerateObject())
            {
                if (!value.TryGetProperty(declared.Name, out _))
                {
                    errors.Add($"{path}.{declared.Name}: declared in the schema but never written");
                }
            }
        }

        private static bool MatchesType(JsonElement type, JsonElement value)
        {
            var types = type.ValueKind == JsonValueKind.Array ? type.EnumerateArray().Select(item => item.GetString()!) : [type.GetString()!];
            return types.Any(name => name switch
            {
                "null" => value.ValueKind == JsonValueKind.Null,
                "object" => value.ValueKind == JsonValueKind.Object,
                "array" => value.ValueKind == JsonValueKind.Array,
                "string" => value.ValueKind == JsonValueKind.String,
                "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
                "number" => value.ValueKind == JsonValueKind.Number,
                _ => false,
            });
        }
    }
}
