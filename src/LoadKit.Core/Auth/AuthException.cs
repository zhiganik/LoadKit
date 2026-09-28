namespace LoadKit.Core.Auth;

/// <summary>
/// A token could not be acquired. <see cref="Exception.Message"/> says what happened, <see cref="Hint"/> what to check.
/// Neither contains secrets, but callers still pass both through <see cref="SecretMasker"/> before output.
/// </summary>
public sealed class AuthException : Exception
{
    public AuthException(string message, string? hint, Exception? innerException = null)
        : base(message, innerException)
    {
        Hint = hint;
    }

    public string? Hint { get; }
}
