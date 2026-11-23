using System.Numerics;

namespace Serilog.Sinks.TriggerBuffer.Benchmarks;

/// <summary>
/// Records latencies in nanoseconds without storing every sample. Buckets grow logarithmically with eight steps per
/// power of two, so a percentile is rounded up to its bucket edge and is at most 12.5 percent too high. The maximum
/// is exact. One instance belongs to one thread, instances are merged afterwards.
/// </summary>
internal sealed class LatencyHistogram
{
    private const int SubBucketBits = 3;
    private const int SubBucketCount = 1 << SubBucketBits;
    private const int BucketCount = 320; // covers values up to about 2^40 ns, which is far above any latency here

    private readonly long[] _counts = new long[BucketCount];

    public long Count { get; private set; }

    public long SumNanoseconds { get; private set; }

    public long MaxNanoseconds { get; private set; }

    public double MeanNanoseconds => Count == 0 ? 0 : (double)SumNanoseconds / Count;

    public void Record(long nanoseconds)
    {
        var value = Math.Max(nanoseconds, 0);
        _counts[Math.Min(IndexOf(value), BucketCount - 1)]++;
        Count++;
        SumNanoseconds += value;
        if (value > MaxNanoseconds)
        {
            MaxNanoseconds = value;
        }
    }

    public void Add(LatencyHistogram other)
    {
        for (var i = 0; i < BucketCount; i++)
        {
            _counts[i] += other._counts[i];
        }

        Count += other.Count;
        SumNanoseconds += other.SumNanoseconds;
        MaxNanoseconds = Math.Max(MaxNanoseconds, other.MaxNanoseconds);
    }

    public long Percentile(double percentile)
    {
        if (Count == 0)
        {
            return 0;
        }

        var target = (long)Math.Ceiling(Count * percentile / 100.0);
        long cumulative = 0;
        for (var i = 0; i < BucketCount; i++)
        {
            cumulative += _counts[i];
            if (cumulative >= target)
            {
                return Math.Min(UpperBound(i), MaxNanoseconds);
            }
        }

        return MaxNanoseconds;
    }

    private static int IndexOf(long value)
    {
        if (value < SubBucketCount)
        {
            return (int)value;
        }

        var highestBit = 63 - BitOperations.LeadingZeroCount((ulong)value);
        var sub = (int)((value >> (highestBit - SubBucketBits)) & (SubBucketCount - 1));
        return ((highestBit - SubBucketBits + 1) << SubBucketBits) + sub;
    }

    private static long LowerBound(int index)
    {
        if (index < SubBucketCount)
        {
            return index;
        }

        var shift = (index >> SubBucketBits) - 1;
        return (long)(SubBucketCount + (index & (SubBucketCount - 1))) << shift;
    }

    private static long UpperBound(int index) => LowerBound(index + 1) - 1;
}
