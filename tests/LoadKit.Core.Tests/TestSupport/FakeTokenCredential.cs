using Azure.Core;

namespace LoadKit.Core.Tests.TestSupport;

/// <summary>Stands in for Azure CLI / DefaultAzureCredential; tests never call Entra ID.</summary>
internal sealed class FakeTokenCredential(Func<TokenRequestContext, AccessToken>? getToken = null) : TokenCredential
{
    private readonly Func<TokenRequestContext, AccessToken> _getToken =
        getToken ?? (_ => new AccessToken("fake-azure-token", DateTimeOffset.UtcNow.AddHours(1)));

    public List<string[]> RequestedScopes { get; } = [];

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        RequestedScopes.Add(requestContext.Scopes);
        return _getToken(requestContext);
    }

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }
}
