namespace LoadKit.Core.Scenarios.Model;

public sealed record AzureIdentityAuth(string Scope, string Source) : AuthOptions
{
    public override string Type => AuthTypeNames.AzureIdentity;
}
