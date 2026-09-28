namespace LoadKit.Core.Tests.TestSupport;

/// <summary>Returns the given values from <see cref="NextInt64(long)"/>, in order.</summary>
internal sealed class SequenceRandom(params long[] values) : Random
{
    private int _index;

    public override long NextInt64(long maxValue)
    {
        var value = values[_index++];
        Assert.InRange(value, 0, maxValue - 1);
        return value;
    }
}
