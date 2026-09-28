namespace LoadKit.Cli;

/// <summary>Public contract: see README.md and docs/user/CLI_REFERENCE.md.</summary>
internal static class ExitCodes
{
    public const int Success = 0;
    public const int ThresholdsFailed = 1;
    public const int InvalidScenario = 2;
    public const int PreflightFailed = 3;
    public const int ConfirmationRequired = 4;
    public const int Interrupted = 130;
}
