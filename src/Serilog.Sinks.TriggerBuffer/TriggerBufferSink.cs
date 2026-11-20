using System.Collections.Generic;
using Serilog.Core;
using Serilog.Events;

namespace Serilog.Sinks.TriggerBuffer;

internal sealed class TriggerBufferSink : ILogEventSink
{
    private readonly ILogEventSink _target;
    private readonly LogEventLevel _bufferMinimumLevel;
    private readonly LogEventLevel _triggerLevel;
    private readonly Queue<LogEvent> _buffer = new();

    public TriggerBufferSink(ILogEventSink target, LogEventLevel bufferMinimumLevel, LogEventLevel triggerLevel)
    {
        _target = target;
        _bufferMinimumLevel = bufferMinimumLevel;
        _triggerLevel = triggerLevel;
    }

    public void Emit(LogEvent logEvent)
    {
        if (logEvent.Level < _bufferMinimumLevel)
        {
            return;
        }

        if (logEvent.Level < _triggerLevel)
        {
            _buffer.Enqueue(logEvent);
            return;
        }

        while (_buffer.Count > 0)
        {
            _target.Emit(_buffer.Dequeue());
        }

        _target.Emit(logEvent);
    }
}
