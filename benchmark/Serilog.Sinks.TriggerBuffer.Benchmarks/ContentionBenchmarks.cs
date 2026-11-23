using BenchmarkDotNet.Attributes;
using Serilog.Core;

namespace Serilog.Sinks.TriggerBuffer.Benchmarks;

/// <summary>
/// Scenario B: the hot path with several threads logging at the same time. One invocation is
/// <see cref="Threads"/> times <see cref="EventsPerThread"/> events, so the time per event is the reported time
/// divided by that number. The reference is the same logger with a no-op sink.
/// </summary>
[MemoryDiagnoser]
public class ContentionBenchmarks
{
    private const int EventsPerThread = 20_000;
    private const int BufferSize = 1000;

    private Logger _plain = null!;
    private Logger _buffered = null!;

    [Params(1, 8)]
    public int Threads { get; set; }

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
    public void NullSinkOnly() => LogFromThreads(_plain);

    [Benchmark]
    public void Buffered() => LogFromThreads(_buffered);

    // Dedicated threads, so that the requested number really run in parallel and do not depend on the thread pool.
    private void LogFromThreads(Logger logger)
    {
        var tasks = new Task[Threads];
        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Factory.StartNew(
                () => LogEvents(logger),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        Task.WaitAll(tasks);
    }

    private static void LogEvents(Logger logger)
    {
        for (var i = 0; i < EventsPerThread; i++)
        {
            logger.Debug(BenchmarkLoggers.Template, 42, "Contoso", 17);
        }
    }
}
