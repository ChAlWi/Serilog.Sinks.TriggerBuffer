using Serilog.Core;
using Serilog.Events;

namespace Serilog.Sinks.TriggerBuffer.Benchmarks;

/// <summary>A sink that accepts every event and does nothing with it. Used as the reference for the baseline.</summary>
internal sealed class NullSink : ILogEventSink
{
    public void Emit(LogEvent logEvent)
    {
    }
}
