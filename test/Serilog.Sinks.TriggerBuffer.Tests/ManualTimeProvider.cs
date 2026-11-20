namespace Serilog.Sinks.TriggerBuffer.Tests;

/// <summary>A time provider whose monotonic clock only moves when the test advances it.</summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private long _timestamp;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => Interlocked.Read(ref _timestamp);

    public void Advance(TimeSpan amount) => Interlocked.Add(ref _timestamp, amount.Ticks);
}
