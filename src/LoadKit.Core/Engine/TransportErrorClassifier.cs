using System.Security.Authentication;
using LoadKit.Core.Metrics;

namespace LoadKit.Core.Engine;

/// <summary>Maps an exception from sending a request or reading its body to an <see cref="ErrorKind"/>.</summary>
internal static class TransportErrorClassifier
{
    public static ErrorKind Classify(Exception exception)
    {
        return exception switch
        {
            HttpRequestException { HttpRequestError: HttpRequestError.SecureConnectionError } => ErrorKind.Tls,
            HttpRequestException { InnerException: AuthenticationException } => ErrorKind.Tls,
            HttpRequestException
            {
                HttpRequestError: HttpRequestError.ConnectionError or HttpRequestError.NameResolutionError or HttpRequestError.ProxyTunnelError,
            } => ErrorKind.Connection,

            // HttpIOException and other IOExceptions: the connection broke while reading the body.
            IOException => ErrorKind.Connection,
            _ => ErrorKind.Other,
        };
    }
}
