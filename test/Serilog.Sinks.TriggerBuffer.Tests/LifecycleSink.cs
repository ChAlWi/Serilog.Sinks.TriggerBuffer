using Serilog.Core;
using Serilog.Events;

namespace Serilog.Sinks.TriggerBuffer.Tests;

/// <summary>
/// Records received events and the way it was closed in one journal, so tests can check what happened before
/// the close and how often the target was closed.
/// </summary>
internal sealed class LifecycleSink : ILogEventSink, IDisposable, IAsyncDisposable
{
    public const string ClosedSync = "<closed sync>";
    public const string ClosedAsync = "<closed async>";

    private readonly object _gate = new();
    private readonly List<string> _journal = [];

    public IReadOnlyList<string> Journal
    {
        get
        {
            lock (_gate)
            {
                return _journal.ToArray();
            }
        }
    }

    public void Emit(LogEvent logEvent) => Add(logEvent.RenderMessage());

    public void Dispose() => Add(ClosedSync);

    public ValueTask DisposeAsync()
    {
        Add(ClosedAsync);
        return ValueTask.CompletedTask;
    }

    private void Add(string entry)
    {
        lock (_gate)
        {
            _journal.Add(entry);
        }
    }
}
