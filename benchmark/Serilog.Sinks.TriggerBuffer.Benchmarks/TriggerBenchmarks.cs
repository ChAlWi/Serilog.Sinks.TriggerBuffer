using BenchmarkDotNet.Attributes;
using Serilog.Core;

namespace Serilog.Sinks.TriggerBuffer.Benchmarks;

/// <summary>
/// Scenario C: the cost of one trigger. Each invocation logs <see cref="BufferedEvents"/> events below the trigger
/// level and then one error. The target receives the same events in both variants, so the difference between them is
/// the cost of buffering and of the handover. The buffer is empty again after every invocation, so invocations do not
/// influence each other. The target does nothing, so target latency is not part of this scenario.
/// </summary>
[MemoryDiagnoser]
public class TriggerBenchmarks
{
    private const int BufferSize = 1000;

    private Logger _plain = null!;
    private Logger _buffered = null!;

    [Params(10, 100, 1000)]
    public int BufferedEvents { get; set; }

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
    public void PassThrough() => LogThenError(_plain);

    [Benchmark]
    public void BufferThenTrigger() => LogThenError(_buffered);

    private void LogThenError(Logger logger)
    {
        for (var i = 0; i < BufferedEvents; i++)
        {
            logger.Debug(BenchmarkLoggers.Template, 42, "Contoso", 17);
        }

        logger.Error(BenchmarkLoggers.Template, 42, "Contoso", 17);
    }
}
