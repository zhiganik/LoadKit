namespace LoadKit.Core.Scenarios.Validation;

internal enum FieldKind
{
    String,
    Integer,
    Number,
    Boolean,

    /// <summary>Object whose values are strings (headers).</summary>
    StringMap,

    /// <summary>Object whose values are strings, numbers or booleans (query).</summary>
    ScalarMap,

    IntegerArray,

    /// <summary>Any JSON object, not validated further (request body).</summary>
    JsonObject,

    /// <summary>Object described by <see cref="FieldSpec.Object"/>.</summary>
    Object,

    /// <summary>Array of objects described by <see cref="FieldSpec.Object"/>.</summary>
    ObjectArray,

    /// <summary>Object whose spec is chosen by its <c>type</c> field from <see cref="ScenarioFormat.AuthTypes"/>.</summary>
    Auth,
}
