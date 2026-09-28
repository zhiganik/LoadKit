using System.Net;
using LoadKit.Core.Auth;

namespace LoadKit.Core.Engine;

/// <summary>
/// One <see cref="HttpClient"/> per run: <c>HttpClient → AuthHandler → SocketsHttpHandler</c>.
/// <c>run</c> and <c>check</c> use the same pipeline.
/// </summary>
public static class HttpPipelineFactory
{
    private static readonly TimeSpan PooledConnectionLifetime = TimeSpan.FromMinutes(2);

    /// <param name="authProvider">Null when the scenario has no auth.</param>
    public static HttpClient Create(int concurrency, IAuthProvider? authProvider)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(concurrency, 1);
        var socketsHandler = new SocketsHttpHandler
        {
            MaxConnectionsPerServer = concurrency,
            PooledConnectionLifetime = PooledConnectionLifetime,
            AutomaticDecompression = DecompressionMethods.All,
        };

        HttpMessageHandler handler = authProvider is null
            ? socketsHandler
            : new AuthHandler(authProvider) { InnerHandler = socketsHandler };

        // Per-request timeouts are applied by the engine so they can be classified as ErrorKind.Timeout.
        return new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
    }
}
