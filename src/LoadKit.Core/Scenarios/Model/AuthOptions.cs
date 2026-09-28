namespace LoadKit.Core.Scenarios.Model;

/// <summary>Base of all <c>auth</c> variants. <see cref="Type"/> is the <c>auth.type</c> value.</summary>
public abstract record AuthOptions
{
    public abstract string Type { get; }
}
