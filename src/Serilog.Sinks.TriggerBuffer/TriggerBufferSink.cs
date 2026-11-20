using System;
using System.Collections.Generic;
using System.Threading;
using Serilog.Core;
using Serilog.Debugging;
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
    private readonly object _gate = new();
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
        // The lock is entered by this thread only while the target is being called. An event that comes back
        // into this sink from there would change the block that is being handed over, so it is not accepted.
        if (Monitor.IsEntered(_gate))
        {
            SelfLog.WriteLine(
                "TriggerBuffer: a {0} event was logged back into the sink from its target sink and was ignored",
                logEvent.Level);
            return;
        }

        if (logEvent.Level < _bufferMinimumLevel)
        {
            return;
        }

        // Intake and handover share one lock, so a block of events is never interleaved with another caller's events.
        lock (_gate)
        {
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
                TryEmit(_buffer.Dequeue().Event);
            }

            TryEmit(logEvent);
        }
    }

    private void TryEmit(LogEvent logEvent)
    {
        try
        {
            _target.Emit(logEvent);
        }
        catch (Exception exception)
        {
            // Report the failure without repeating the event payload, and keep delivering the rest of the block.
            SelfLog.WriteLine(
                "TriggerBuffer: the target sink failed to accept a {0} event: {1}",
                logEvent.Level,
                exception);
        }
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
