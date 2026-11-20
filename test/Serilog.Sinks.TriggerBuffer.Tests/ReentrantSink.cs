using Serilog.Core;
using Serilog.Events;

namespace Serilog.Sinks.TriggerBuffer.Tests;

/// <summary>Logs back into the logger it is attached to when it receives the "trigger" message.</summary>
internal sealed class ReentrantSink : ILogEventSink
{
    private readonly CollectingSink _received = new();

    public ILogger? Logger { get; set; }

    public IReadOnlyList<string> Messages => _received.Messages;

    public void Emit(LogEvent logEvent)
    {
        _received.Emit(logEvent);

        if (logEvent.RenderMessage() == "trigger")
        {
            Logger?.Information("from target");
        }
    }
}
