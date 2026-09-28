namespace LoadKit.Core.Tests.TestSupport;

/// <summary>Answers requests with a delegate and records what was sent. Thread-safe.</summary>
internal sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respondAsync)
    : HttpMessageHandler
{
    private int _requestCount;

    public int RequestCount => Volatile.Read(ref _requestCount);

    public static FakeHttpMessageHandler Returning(System.Net.HttpStatusCode statusCode, string body = "")
    {
        return new FakeHttpMessageHandler((_, _) => Task.FromResult(Response(statusCode, body)));
    }

    public static HttpResponseMessage Response(System.Net.HttpStatusCode statusCode, string body = "")
    {
        return new HttpResponseMessage(statusCode) { Content = new StringContent(body) };
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _requestCount);
        return respondAsync(request, cancellationToken);
    }
}
