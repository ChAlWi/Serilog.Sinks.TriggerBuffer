using BenchmarkDotNet.Attributes;
using Serilog.Core;

namespace Serilog.Sinks.TriggerBuffer.Benchmarks;

/// <summary>
/// Scenario 0: what Serilog itself costs for one event, with no sink and with a sink that does nothing.
/// Every later scenario reports its cost relative to these two numbers.
/// </summary>
[MemoryDiagnoser]
public class BaselineBenchmarks
{
    private const string Template = "Loaded {OrderId} for {Customer} in {Elapsed} ms";

    private Logger _withoutSink = null!;
    private Logger _withNullSink = null!;

    [GlobalSetup]
    public void Setup()
    {
        _withoutSink = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .CreateLogger();

        _withNullSink = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(new NullSink())
            .CreateLogger();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _withoutSink.Dispose();
        _withNullSink.Dispose();
    }

    [Benchmark(Baseline = true)]
    public void NoSink() => _withoutSink.Debug(Template, 42, "Contoso", 17);

    [Benchmark]
    public void NullSinkOnly() => _withNullSink.Debug(Template, 42, "Contoso", 17);
}
