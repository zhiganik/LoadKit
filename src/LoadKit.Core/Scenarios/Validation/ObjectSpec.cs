namespace LoadKit.Core.Scenarios.Validation;

/// <summary>Allowed fields of one scenario object. <see cref="Name"/> matches the <c>$defs</c> key in the JSON schema.</summary>
internal sealed record ObjectSpec(string Name, IReadOnlyList<FieldSpec> Fields)
{
    public FieldSpec? FindField(string name)
    {
        foreach (var field in Fields)
        {
            if (string.Equals(field.Name, name, StringComparison.Ordinal))
            {
                return field;
            }
        }

        return null;
    }
}
