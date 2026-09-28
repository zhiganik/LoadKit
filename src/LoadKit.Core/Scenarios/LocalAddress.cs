using System.Net;

namespace LoadKit.Core.Scenarios;

/// <summary>Decides whether a URL targets this machine. Anything else needs user confirmation before load.</summary>
public static class LocalAddress
{
    public static bool IsLocal(Uri uri)
    {
        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(uri.Host.Trim('[', ']'), out var address) && IPAddress.IsLoopback(address);
    }
}
