using Serilog.Core;
using Serilog.Events;

namespace Serilog.Sinks.TriggerBuffer.Tests;

/// <summary>Throws when it receives the given message and records only how it was closed.</summary>
internal sealed class FailingLifecycleSink(string failOn) : ILogEventSink, IDisposable
{
    private readonly LifecycleSink _inner = new();

    public IReadOnlyList<string> Journal => _inner.Journal;

    public void Emit(LogEvent logEvent)
    {
        if (logEvent.RenderMessage() == failOn)
        {
            throw new InvalidOperationException("The target sink failed.");
        }

        _inner.Emit(logEvent);
    }

    public void Dispose() => _inner.Dispose();
}
