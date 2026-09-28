using LoadKit.Core.Scenarios;

namespace LoadKit.Core.Tests.Scenarios;

public sealed class LocalAddressTests
{
    [Theory]
    [InlineData("http://localhost:5080", true)]
    [InlineData("https://LOCALHOST", true)]
    [InlineData("http://127.0.0.1:5080", true)]
    [InlineData("http://127.10.0.1", true)]
    [InlineData("http://[::1]:5080", true)]
    [InlineData("http://0.0.0.0:5080", false)]
    [InlineData("http://192.168.1.10", false)]
    [InlineData("https://localhost.example.com", false)]
    [InlineData("https://my-func.azurewebsites.net", false)]
    public void IsLocal_OnlyForLoopback(string url, bool expected)
    {
        Assert.Equal(expected, LocalAddress.IsLocal(new Uri(url)));
    }
}
