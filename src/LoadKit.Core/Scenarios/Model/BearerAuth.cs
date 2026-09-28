namespace LoadKit.Core.Scenarios.Model;

public sealed record BearerAuth(string Token, string Header, string Format) : AuthOptions
{
    public override string Type => AuthTypeNames.Bearer;

    public override string ToString()
    {
        return $"BearerAuth {{ Header = {Header} }}";
    }
}
