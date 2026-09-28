namespace LoadKit.Core.Scenarios;

/// <summary>
/// One validation finding. <see cref="Path"/> is a JSON path such as <c>requests[0].body</c>
/// (<c>$</c> for the whole file). <see cref="Code"/> is one of <see cref="ValidationCodes"/>.
/// </summary>
public sealed record ValidationIssue(ValidationSeverity Severity, string Code, string Path, string Message, string? Hint)
{
    public static ValidationIssue Error(string code, string path, string message, string? hint = null)
    {
        return new ValidationIssue(ValidationSeverity.Error, code, path, message, hint);
    }

    public static ValidationIssue Warning(string code, string path, string message, string? hint = null)
    {
        return new ValidationIssue(ValidationSeverity.Warning, code, path, message, hint);
    }

    public static ValidationIssue Info(string code, string path, string message, string? hint = null)
    {
        return new ValidationIssue(ValidationSeverity.Info, code, path, message, hint);
    }
}
