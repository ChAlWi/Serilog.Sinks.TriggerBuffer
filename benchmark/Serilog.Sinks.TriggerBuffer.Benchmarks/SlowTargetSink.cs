using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace Serilog.Sinks.TriggerBuffer.Benchmarks;

/// <summary>
/// A target that needs a fixed time for every event. It waits by spinning on a stopwatch: <c>Thread.Sleep</c> is far
/// too coarse on Windows for delays of 50 µs to 1 ms. The price is that the waiting thread keeps a core busy.
/// </summary>
internal sealed class SlowTargetSink : ILogEventSink
{
    private readonly long _delayTicks;
    private long _emitted;

    public SlowTargetSink(TimeSpan delay)
    {
        _delayTicks = (long)(delay.TotalSeconds * Stopwatch.Frequency);
    }

    public long EmittedCount => Interlocked.Read(ref _emitted);

    public void Emit(LogEvent logEvent)
    {
        if (_delayTicks > 0)
        {
            SpinWaiter.SpinUntil(Stopwatch.GetTimestamp() + _delayTicks);
        }

        Interlocked.Increment(ref _emitted);
    }
}

internal static class SpinWaiter
{
    public static void SpinUntil(long timestamp)
    {
        while (Stopwatch.GetTimestamp() < timestamp)
        {
            Thread.SpinWait(4);
        }
    }
}
