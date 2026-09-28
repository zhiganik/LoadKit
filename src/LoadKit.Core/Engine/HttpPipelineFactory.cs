using System.Net;
using LoadKit.Core.Auth;

namespace LoadKit.Core.Engine;

/// <summary>
/// One <see cref="HttpClient"/> per run: <c>HttpClient → AuthHandler → SocketsHttpHandler</c>.
/// <c>run</c> and <c>check</c> use the same pipeline.
/// </summary>
public static class HttpPipelineFactory
{
    /// <summary>Timeout of token requests and the preflight reachability check.</summary>
    public static readonly TimeSpan UnauthenticatedRequestTimeout = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan PooledConnectionLifetime = TimeSpan.FromMinutes(2);

    /// <param name="authProvider">Null when the scenario has no auth.</param>
    public static HttpClient Create(int concurrency, IAuthProvider? authProvider)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(concurrency, 1);
        var socketsHandler = CreateSocketsHandler(concurrency);
        HttpMessageHandler handler = authProvider is null
            ? socketsHandler
            : new AuthHandler(authProvider) { InnerHandler = socketsHandler };

        // Per-request timeouts are applied by the engine so they can be classified as ErrorKind.Timeout.
        return new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
    }

    /// <summary>For token endpoints and preflight: no auth handler, its own connections, a fixed timeout.</summary>
    public static HttpClient CreateUnauthenticated()
    {
        return new HttpClient(CreateSocketsHandler(maxConnectionsPerServer: 2)) { Timeout = UnauthenticatedRequestTimeout };
    }

    private static SocketsHttpHandler CreateSocketsHandler(int maxConnectionsPerServer)
    {
        return new SocketsHttpHandler
        {
            MaxConnectionsPerServer = maxConnectionsPerServer,
            PooledConnectionLifetime = PooledConnectionLifetime,
            AutomaticDecompression = DecompressionMethods.All,
        };
    }
}
