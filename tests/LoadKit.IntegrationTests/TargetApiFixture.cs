using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using TargetApi;

namespace LoadKit.IntegrationTests;

/// <summary>
/// Runs <c>samples/TargetApi</c> on real Kestrel with a random free port.
/// TestServer does not fit: the CLI makes real network calls.
/// </summary>
public sealed class TargetApiFixture : IAsyncLifetime
{
    private WebApplication? _application;

    public Uri BaseAddress { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        _application = TargetApiApplication.Create(
        [
            "--urls=http://127.0.0.1:0",
            "--environment=Development",
            "--TargetApi:FailProbability=0.5",
        ]);
        await _application.StartAsync();

        var addresses = _application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses;
        BaseAddress = new Uri(addresses.First());
    }

    public HttpClient CreateClient()
    {
        return new HttpClient { BaseAddress = BaseAddress };
    }

    public async ValueTask DisposeAsync()
    {
        if (_application is not null)
        {
            await _application.StopAsync();
            await _application.DisposeAsync();
        }
    }
}
