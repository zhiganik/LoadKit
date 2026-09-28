namespace LoadKit.Core.Scenarios.Validation;

internal sealed record FieldSpec(
    string Name,
    FieldKind Kind,
    bool IsRequired = false,
    ObjectSpec? Object = null,
    string MissingCode = ValidationCodes.RequiredField,
    string? MissingHint = null);
