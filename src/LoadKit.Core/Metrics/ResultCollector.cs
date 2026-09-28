namespace LoadKit.Core.Metrics;

/// <summary>
/// Lock-free storage for measured results. With a known request count the array is preallocated and each
/// measured request writes its own slot; for duration runs each worker appends to its own list.
/// </summary>
internal sealed class ResultCollector
{
    private readonly RequestResult[]? _slots;
    private readonly bool[]? _slotWritten;
    private readonly List<RequestResult>[]? _workerResults;

    private ResultCollector(int? measuredRequestCount, int workerCount)
    {
        if (measuredRequestCount is { } count)
        {
            _slots = new RequestResult[count];
            _slotWritten = new bool[count];
            return;
        }

        _workerResults = new List<RequestResult>[workerCount];
        for (var workerIndex = 0; workerIndex < workerCount; workerIndex++)
        {
            _workerResults[workerIndex] = [];
        }
    }

    public static ResultCollector ForRequestCount(int measuredRequestCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(measuredRequestCount);
        return new ResultCollector(measuredRequestCount, workerCount: 0);
    }

    public static ResultCollector ForDuration(int workerCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(workerCount, 1);
        return new ResultCollector(measuredRequestCount: null, workerCount);
    }

    /// <param name="workerIndex">Index of the worker; each worker writes only its own list.</param>
    /// <param name="measuredIndex">Zero-based number of the measured request (warmup excluded).</param>
    public void Record(int workerIndex, long measuredIndex, RequestResult result)
    {
        if (_slots is not null)
        {
            _slots[measuredIndex] = result;
            _slotWritten![measuredIndex] = true;
            return;
        }

        _workerResults![workerIndex].Add(result);
    }

    /// <summary>All recorded results. Call only after every worker has finished.</summary>
    public RequestResult[] ToArray()
    {
        if (_slots is not null)
        {
            return CollectWrittenSlots();
        }

        var totalCount = 0;
        foreach (var workerResults in _workerResults!)
        {
            totalCount += workerResults.Count;
        }

        var results = new RequestResult[totalCount];
        var offset = 0;
        foreach (var workerResults in _workerResults)
        {
            workerResults.CopyTo(results, offset);
            offset += workerResults.Count;
        }

        return results;
    }

    // An interrupted run leaves some slots empty; they are skipped.
    private RequestResult[] CollectWrittenSlots()
    {
        var writtenCount = 0;
        foreach (var written in _slotWritten!)
        {
            if (written)
            {
                writtenCount++;
            }
        }

        if (writtenCount == _slots!.Length)
        {
            return _slots;
        }

        var results = new RequestResult[writtenCount];
        var resultIndex = 0;
        for (var slotIndex = 0; slotIndex < _slots.Length; slotIndex++)
        {
            if (_slotWritten[slotIndex])
            {
                results[resultIndex++] = _slots[slotIndex];
            }
        }

        return results;
    }
}
