using System;
using System.Collections.Generic;
using System.Linq;

namespace CheckHash.Services;

public record ProcessingOptions(int MaxDegreeOfParallelism, int? BufferSize);

public class ProcessingStrategyService
{
    private const long HeavyFileThreshold = 5L * 1024 * 1024 * 1024; // 5GB
    private const int OneMB = 1024 * 1024;
    private const int FourMB = 4 * 1024 * 1024;
    private const int TwoMB = 2 * 1024 * 1024;

    public ProcessingOptions GetProcessingOptions(IEnumerable<(long Size, HashType Algorithm)> files)
    {
        var items = files as IReadOnlyList<(long Size, HashType Algorithm)> ?? files.ToList();
        var count = items.Count;

        if (count == 0)
        {
            return new ProcessingOptions(Environment.ProcessorCount, null);
        }

        var heavyCount = items.Count(x => x.Size > HeavyFileThreshold);
        var lightCount = count - heavyCount;
        var hasBlake3 = items.Any(x => x.Algorithm == HashType.BLAKE3);

        int concurrency;
        int? bufferSize;

        if (heavyCount == count)
        {
            concurrency = Math.Max(1, Math.Min(Environment.ProcessorCount, 2));
            bufferSize = FourMB;
        }
        else if (lightCount == count)
        {
            concurrency = Environment.ProcessorCount;
            bufferSize = OneMB;
        }
        else
        {
            concurrency = Math.Max(1, Environment.ProcessorCount / 2);
            bufferSize = TwoMB;
        }

        if (hasBlake3)
        {
            bufferSize = null;
        }

        return new ProcessingOptions(concurrency, bufferSize);
    }
}