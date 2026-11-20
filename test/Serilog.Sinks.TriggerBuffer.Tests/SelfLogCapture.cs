using Serilog.Debugging;

namespace Serilog.Sinks.TriggerBuffer.Tests;

/// <summary>Captures SelfLog output for the lifetime of the instance. SelfLog is process-wide.</summary>
internal sealed class SelfLogCapture : IDisposable
{
    private readonly object _gate = new();
    private readonly List<string> _messages = [];

    public SelfLogCapture()
    {
        SelfLog.Enable(message =>
        {
            lock (_gate)
            {
                _messages.Add(message);
            }
        });
    }

    public IReadOnlyList<string> Messages
    {
        get
        {
            lock (_gate)
            {
                return _messages.ToArray();
            }
        }
    }

    public void Dispose() => SelfLog.Disable();
}
