namespace LoadKit.IntegrationTests;

internal sealed record CliResult(int ExitCode, string StandardOutput, string StandardError)
{
    public override string ToString()
    {
        return $"exit {ExitCode}\n{StandardOutput}\n{StandardError}";
    }
}
