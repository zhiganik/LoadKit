using System.Collections.Concurrent;

namespace LoadKit.Core.Metrics;

/// <summary>
/// Keeps up to <c>maxSamplesPerGroup</c> samples per status code and error kind. A worker first reserves a slot
/// (so the body is captured only when it will be kept), then stores the sample. Lock-free.
/// </summary>
internal sealed class ErrorSampleCollector(int maxSamplesPerGroup)
{
    private readonly ConcurrentDictionary<(int StatusCode, ErrorKind Error), SampleGroup> _groups = new();

    public bool TryReserve(int statusCode, ErrorKind error, out int slot)
    {
        var group = _groups.GetOrAdd((statusCode, error), static (_, capacity) => new SampleGroup(capacity), maxSamplesPerGroup);
        return group.TryReserve(out slot);
    }

    public void Store(int slot, ErrorSample sample)
    {
        _groups[(sample.StatusCode, sample.Error)].Store(slot, sample);
    }

    /// <summary>Stored samples ordered by status code, then error kind. Call after every worker has finished.</summary>
    public IReadOnlyList<ErrorSample> ToList()
    {
        var samples = new List<ErrorSample>();
        foreach (var (_, group) in _groups.OrderBy(pair => pair.Key.StatusCode).ThenBy(pair => pair.Key.Error))
        {
            group.CopyTo(samples);
        }

        return samples;
    }

    private sealed class SampleGroup(int capacity)
    {
        private readonly ErrorSample?[] _samples = new ErrorSample?[capacity];
        private int _reservedCount;

        public bool TryReserve(out int slot)
        {
            slot = Interlocked.Increment(ref _reservedCount) - 1;
            return slot < _samples.Length;
        }

        public void Store(int slot, ErrorSample sample)
        {
            _samples[slot] = sample;
        }

        public void CopyTo(List<ErrorSample> target)
        {
            foreach (var sample in _samples)
            {
                if (sample is not null)
                {
                    target.Add(sample);
                }
            }
        }
    }
}
