using Serilog.Core;
using Serilog.Events;

namespace Serilog.Sinks.TriggerBuffer.Tests;

/// <summary>Throws for events whose rendered message matches, and records all others.</summary>
internal sealed class FailingSink(Func<string, bool> failOn) : ILogEventSink
{
    private readonly CollectingSink _accepted = new();

    public IReadOnlyList<string> Messages => _accepted.Messages;

    public void Emit(LogEvent logEvent)
    {
        if (failOn(logEvent.RenderMessage()))
        {
            throw new InvalidOperationException("The target sink failed.");
        }

        _accepted.Emit(logEvent);
    }
}
