using BenchmarkDotNet.Attributes;
using Serilog.Core;

namespace Serilog.Sinks.TriggerBuffer.Benchmarks;

/// <summary>
/// Scenario A: the buffered hot path on one thread. Events stay below the trigger level, so the target never
/// receives anything. After warm-up the buffer is full, so every event also displaces the oldest one.
/// The reference is the same logger with a no-op sink in place of the trigger buffer.
/// </summary>
[MemoryDiagnoser]
public class HotPathBenchmarks
{
    private Logger _plain = null!;
    private Logger _buffered = null!;

    [Params(100, 1000)]
    public int BufferSize { get; set; }

    [Params(ContextProfile.None, ContextProfile.Small, ContextProfile.Medium, ContextProfile.Large)]
    public ContextProfile Profile { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _plain = BenchmarkLoggers.CreatePlain(Profile, new NullSink());
        _buffered = BenchmarkLoggers.CreateBuffered(Profile, new NullSink(), BufferSize);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _plain.Dispose();
        _buffered.Dispose();
    }

    [Benchmark(Baseline = true)]
    public void NullSinkOnly() => _plain.Debug(BenchmarkLoggers.Template, 42, "Contoso", 17);

    [Benchmark]
    public void Buffered() => _buffered.Debug(BenchmarkLoggers.Template, 42, "Contoso", 17);
}
