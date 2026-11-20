using Serilog.Core;
using Serilog.Events;

namespace Serilog.Sinks.TriggerBuffer.Tests;

/// <summary>
/// Blocks inside <see cref="Emit"/> when it receives the configured message until the test releases it.
/// Events are recorded after the block ends, so the recorded order is the order in which handovers completed.
/// </summary>
internal sealed class BlockingSink(string blockOnMessage) : ILogEventSink
{
    private static readonly TimeSpan SafetyTimeout = TimeSpan.FromSeconds(10);

    private readonly CollectingSink _received = new();

    public ManualResetEventSlim Entered { get; } = new(false);

    public ManualResetEventSlim Release { get; } = new(false);

    public IReadOnlyList<string> Messages => _received.Messages;

    public void Emit(LogEvent logEvent)
    {
        if (logEvent.RenderMessage() == blockOnMessage)
        {
            Entered.Set();
            Release.Wait(SafetyTimeout);
        }

        _received.Emit(logEvent);
    }
}
