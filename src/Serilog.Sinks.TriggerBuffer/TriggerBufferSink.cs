using System;
using System.Collections.Generic;
using Serilog.Core;
using Serilog.Events;

namespace Serilog.Sinks.TriggerBuffer;

internal sealed class TriggerBufferSink : ILogEventSink
{
    private readonly ILogEventSink _target;
    private readonly TimeProvider _timeProvider;
    private readonly LogEventLevel _bufferMinimumLevel;
    private readonly LogEventLevel _triggerLevel;
    private readonly int _maximumBufferCount;
    private readonly TimeSpan _maximumBufferAge;
    private readonly Queue<BufferedEvent> _buffer = new();

    public TriggerBufferSink(
        ILogEventSink target,
        TimeProvider timeProvider,
        LogEventLevel bufferMinimumLevel,
        LogEventLevel triggerLevel,
        int maximumBufferCount,
        TimeSpan maximumBufferAge)
    {
        _target = target;
        _timeProvider = timeProvider;
        _bufferMinimumLevel = bufferMinimumLevel;
        _triggerLevel = triggerLevel;
        _maximumBufferCount = maximumBufferCount;
        _maximumBufferAge = maximumBufferAge;
    }

    public void Emit(LogEvent logEvent)
    {
        if (logEvent.Level < _bufferMinimumLevel)
        {
            return;
        }

        var now = _timeProvider.GetTimestamp();
        RemoveExpired(now);

        if (logEvent.Level < _triggerLevel)
        {
            _buffer.Enqueue(new BufferedEvent(logEvent, now));
            while (_buffer.Count > _maximumBufferCount)
            {
                _buffer.Dequeue();
            }

            return;
        }

        while (_buffer.Count > 0)
        {
            _target.Emit(_buffer.Dequeue().Event);
        }

        _target.Emit(logEvent);
    }

    private void RemoveExpired(long now)
    {
        while (_buffer.Count > 0 && _timeProvider.GetElapsedTime(_buffer.Peek().Timestamp, now) > _maximumBufferAge)
        {
            _buffer.Dequeue();
        }
    }

    private readonly record struct BufferedEvent(LogEvent Event, long Timestamp);
}
