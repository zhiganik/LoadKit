using System.Globalization;

namespace LoadKit.Core.Engine;

/// <summary>Creates a <c>loadrun</c> id like <c>20260928-101530-3fa2</c>: sortable and short enough to type in KQL.</summary>
public static class RunIdGenerator
{
    public static string Create(TimeProvider timeProvider, Random random)
    {
        var timestamp = timeProvider.GetUtcNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        return $"{timestamp}-{random.Next(0x10000):x4}";
    }
}
