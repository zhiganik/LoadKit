namespace LoadKit.Core.Scenarios;

public enum ValidationSeverity
{
    /// <summary>The scenario cannot run.</summary>
    Error,

    /// <summary>The scenario runs, but probably not as intended.</summary>
    Warning,

    /// <summary>Something the user should know, for example that <c>run</c> will ask for confirmation.</summary>
    Info,
}
