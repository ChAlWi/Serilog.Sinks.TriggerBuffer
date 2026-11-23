using Serilog.Core;
using Serilog.Events;

namespace Serilog.Sinks.TriggerBuffer.Benchmarks;

/// <summary>Keeps every event it receives, so that the verify mode can inspect what reached the target.</summary>
internal sealed class RecordingSink : ILogEventSink
{
    public List<LogEvent> Events { get; } = [];

    public void Emit(LogEvent logEvent) => Events.Add(logEvent);
}
