using System.Text.Json;
using System.Text.Json.Nodes;

namespace LoadKit.Core.Scenarios.Validation;

/// <summary>Checks unknown fields, types and required fields against <see cref="ScenarioFormat"/>. Collects all issues.</summary>
internal sealed class StructureValidator(ICollection<ValidationIssue> issues)
{
    public void ValidateObject(JsonNode? node, ObjectSpec spec, string path)
    {
        if (node is not JsonObject jsonObject)
        {
            AddInvalidType(path, FieldKind.Object);
            return;
        }

        foreach (var (name, value) in jsonObject)
        {
            var childPath = JsonPath.Child(path, name);
            var field = spec.FindField(name);
            if (field is null)
            {
                issues.Add(ValidationIssue.Error(ValidationCodes.UnknownField, childPath, $"unknown field '{name}'", UnknownFieldHint(spec, name)));
                continue;
            }

            ValidateField(value, field, childPath);
        }

        foreach (var field in spec.Fields)
        {
            if (field.IsRequired && !jsonObject.ContainsKey(field.Name))
            {
                var childPath = JsonPath.Child(path, field.Name);
                issues.Add(ValidationIssue.Error(field.MissingCode, childPath, $"{childPath} is required", field.MissingHint));
            }
        }
    }

    private void ValidateField(JsonNode? value, FieldSpec field, string path)
    {
        switch (field.Kind)
        {
            case FieldKind.Object:
                ValidateObject(value, field.Object!, path);
                break;
            case FieldKind.ObjectArray:
                ValidateObjectArray(value, field.Object!, path);
                break;
            case FieldKind.Auth:
                ValidateAuth(value, path);
                break;
            case FieldKind.StringMap:
                ValidateMap(value, path, FieldKind.StringMap);
                break;
            case FieldKind.ScalarMap:
                ValidateMap(value, path, FieldKind.ScalarMap);
                break;
            case FieldKind.IntegerArray:
                ValidateIntegerArray(value, path);
                break;
            default:
                if (!IsScalarOfKind(value, field.Kind))
                {
                    AddInvalidType(path, field.Kind);
                }

                break;
        }
    }

    private void ValidateObjectArray(JsonNode? value, ObjectSpec itemSpec, string path)
    {
        if (value is not JsonArray array)
        {
            AddInvalidType(path, FieldKind.ObjectArray);
            return;
        }

        for (var index = 0; index < array.Count; index++)
        {
            ValidateObject(array[index], itemSpec, JsonPath.Index(path, index));
        }
    }

    private void ValidateIntegerArray(JsonNode? value, string path)
    {
        if (value is not JsonArray array)
        {
            AddInvalidType(path, FieldKind.IntegerArray);
            return;
        }

        for (var index = 0; index < array.Count; index++)
        {
            if (!JsonNodeReader.IsInteger(array[index], out _))
            {
                AddInvalidType(JsonPath.Index(path, index), FieldKind.Integer);
            }
        }
    }

    private void ValidateMap(JsonNode? value, string path, FieldKind mapKind)
    {
        if (value is not JsonObject map)
        {
            AddInvalidType(path, mapKind);
            return;
        }

        foreach (var (name, entry) in map)
        {
            var isValid = mapKind == FieldKind.StringMap
                ? JsonNodeReader.IsString(entry, out _)
                : entry is JsonValue && entry.GetValueKind() is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False;
            if (!isValid)
            {
                var entryPath = JsonPath.Child(path, name);
                var expected = mapKind == FieldKind.StringMap ? "a string" : "a string, number or boolean";
                issues.Add(ValidationIssue.Error(ValidationCodes.InvalidType, entryPath, $"{entryPath} must be {expected}"));
            }
        }
    }

    private void ValidateAuth(JsonNode? value, string path)
    {
        if (value is not JsonObject auth)
        {
            AddInvalidType(path, FieldKind.Object);
            return;
        }

        var typePath = JsonPath.Child(path, "type");
        if (!auth.ContainsKey("type"))
        {
            issues.Add(ValidationIssue.Error(ValidationCodes.RequiredField, typePath, $"{typePath} is required", "use one of: " + ScenarioFormat.AuthTypeList));
            return;
        }

        if (!JsonNodeReader.IsString(auth["type"], out var type))
        {
            AddInvalidType(typePath, FieldKind.String);
            return;
        }

        if (!ScenarioFormat.AuthTypes.TryGetValue(type, out var authSpec))
        {
            issues.Add(ValidationIssue.Error(ValidationCodes.InvalidValue, typePath, $"unknown auth type '{type}'", "use one of: " + ScenarioFormat.AuthTypeList));
            return;
        }

        ValidateObject(auth, authSpec, path);
    }

    private void AddInvalidType(string path, FieldKind expectedKind)
    {
        issues.Add(ValidationIssue.Error(ValidationCodes.InvalidType, path, $"{path} must be {Describe(expectedKind)}"));
    }

    private static bool IsScalarOfKind(JsonNode? value, FieldKind kind)
    {
        return kind switch
        {
            FieldKind.String => JsonNodeReader.IsString(value, out _),
            FieldKind.Integer => JsonNodeReader.IsInteger(value, out _),
            FieldKind.Number => JsonNodeReader.IsNumber(value, out _),
            FieldKind.Boolean => JsonNodeReader.IsBoolean(value, out _),
            FieldKind.JsonObject => value is JsonObject,
            _ => false,
        };
    }

    private static string Describe(FieldKind kind)
    {
        return kind switch
        {
            FieldKind.String => "a string",
            FieldKind.Integer => "an integer",
            FieldKind.Number => "a number",
            FieldKind.Boolean => "true or false",
            FieldKind.StringMap => "an object with string values",
            FieldKind.ScalarMap => "an object with string, number or boolean values",
            FieldKind.IntegerArray => "an array of integers",
            FieldKind.ObjectArray => "an array of objects",
            _ => "an object",
        };
    }

    private static string UnknownFieldHint(ObjectSpec spec, string unknownName)
    {
        var closest = spec.Fields
            .Select(field => (field.Name, Distance: TextDistance.Levenshtein(unknownName.ToLowerInvariant(), field.Name.ToLowerInvariant())))
            .Where(candidate => candidate.Distance <= 2)
            .OrderBy(candidate => candidate.Distance)
            .Select(candidate => candidate.Name)
            .FirstOrDefault();
        return closest is not null
            ? $"did you mean '{closest}'?"
            : "the field is not in the scenario format; remove it (allowed: " + string.Join(", ", spec.Fields.Select(field => field.Name)) + ")";
    }
}
