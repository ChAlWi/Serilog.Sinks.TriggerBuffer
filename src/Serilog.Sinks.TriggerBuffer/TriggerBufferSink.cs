using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;

namespace Serilog.Sinks.TriggerBuffer;

// The wrapper implements all optional Serilog sink interfaces on purpose. Serilog's Wrap helper then does not add its
// own forwarding sink, and the target is closed exactly once, by this class.
internal sealed class TriggerBufferSink : ILogEventSink, ISetLoggingFailureListener, IDisposable, IAsyncDisposable
{
    private readonly ILogEventSink _target;
    private readonly TimeProvider _timeProvider;
    private readonly LogEventLevel _bufferMinimumLevel;
    private readonly LogEventLevel _triggerLevel;
    private readonly int _maximumBufferCount;
    private readonly TimeSpan _maximumBufferAge;
    private readonly bool _flushOnShutdown;
    private readonly object _gate = new();
    private readonly Queue<BufferedEvent> _buffer = new();
    private bool _disposed;

    public TriggerBufferSink(
        ILogEventSink target,
        TimeProvider timeProvider,
        LogEventLevel bufferMinimumLevel,
        LogEventLevel triggerLevel,
        int maximumBufferCount,
        TimeSpan maximumBufferAge,
        bool flushOnShutdown)
    {
        _target = target;
        _timeProvider = timeProvider;
        _bufferMinimumLevel = bufferMinimumLevel;
        _triggerLevel = triggerLevel;
        _maximumBufferCount = maximumBufferCount;
        _maximumBufferAge = maximumBufferAge;
        _flushOnShutdown = flushOnShutdown;
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
            if (_disposed)
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

            HandOverBuffer();
            TryEmit(logEvent);
        }
    }

    public void SetFailureListener(ILoggingFailureListener failureListener)
    {
        (_target as ISetLoggingFailureListener)?.SetFailureListener(failureListener);
    }

    public void Dispose()
    {
        if (!StopAcceptingEvents())
        {
            return;
        }

        (_target as IDisposable)?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (!StopAcceptingEvents())
        {
            return;
        }

        if (_target is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            (_target as IDisposable)?.Dispose();
        }
    }

    // Returns false if the sink was already stopped, so that the target is closed only once.
    private bool StopAcceptingEvents()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return false;
            }

            _disposed = true;

            if (_flushOnShutdown)
            {
                RemoveExpired(_timeProvider.GetTimestamp());
                HandOverBuffer();
            }
            else
            {
                _buffer.Clear();
            }

            return true;
        }
    }

    private void HandOverBuffer()
    {
        while (_buffer.Count > 0)
        {
            TryEmit(_buffer.Dequeue().Event);
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
