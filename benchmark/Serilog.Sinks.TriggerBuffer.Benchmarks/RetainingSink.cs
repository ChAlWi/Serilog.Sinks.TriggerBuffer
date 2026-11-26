using Serilog.Core;
using Serilog.Events;

namespace Serilog.Sinks.TriggerBuffer.Benchmarks;

/// <summary>
/// The control for the hot path. It keeps the last <c>capacity</c> events in a ring of references and does nothing
/// else: no lock, no timestamp, no expiry check, no queue. Any buffer that holds events has at least the cost of
/// keeping them alive, so this is the least a buffering sink can cost. The difference between this control and the
/// trigger buffer is what the sink's own logic adds. The difference between this control and a no-op sink is the
/// price of retention alone, which is mostly garbage collection.
/// <para>
/// It is safe to call from several threads, but two threads may overwrite the same slot. That does not matter here,
/// the control is not used to read events back (except in the verify mode, on one thread).
/// </para>
/// </summary>
internal sealed class RetainingSink : ILogEventSink
{
    private readonly LogEvent?[] _ring;
    private int _next = -1;

    public RetainingSink(int capacity)
    {
        _ring = new LogEvent?[capacity];
    }

    public void Emit(LogEvent logEvent)
    {
        var slot = (Interlocked.Increment(ref _next) & int.MaxValue) % _ring.Length;
        _ring[slot] = logEvent;
    }

    /// <summary>The events that are currently held. For the verify mode, not for the benchmarks.</summary>
    public IReadOnlyList<LogEvent> Retained() => _ring.OfType<LogEvent>().ToList();
}
