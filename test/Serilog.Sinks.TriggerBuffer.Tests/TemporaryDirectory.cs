namespace Serilog.Sinks.TriggerBuffer.Tests;

internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TriggerBufferTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best effort clean-up. The tests assert separately that no file stays locked.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
