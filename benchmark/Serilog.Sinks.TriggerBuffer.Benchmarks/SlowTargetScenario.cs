using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Serilog.Core;

namespace Serilog.Sinks.TriggerBuffer.Benchmarks;

/// <summary>How one run of scenario D is set up.</summary>
internal sealed record SlowTargetConfig(
    TimeSpan TargetDelay,
    TimeSpan Duration,
    TimeSpan FirstTrigger,
    TimeSpan TriggerInterval,
    int LogThreads = 4,
    int EventsPerSecondPerThread = 5000,
    int BufferSize = 500);

/// <summary>What one run of scenario D measured.</summary>
internal sealed record SlowTargetResult(
    LatencyHistogram LogLatency,
    LatencyHistogram HandoverTime,
    long EventsLogged,
    int Triggers,
    long TargetEvents);

/// <summary>
/// Scenario D: what a slow target costs the other threads. Several threads log below the trigger level at a fixed
/// rate while one thread sends an error at fixed intervals. The handover of the buffered events to the target holds
/// the sink's lock, so logging calls on the other threads have to wait for it.
/// <para>
/// The latency of a call is measured from the moment it was due, not from the moment it started. A thread that was
/// blocked for half a second therefore records every call it missed with the delay it really had (this avoids the
/// "coordinated omission" error, where a blocked thread would only record one slow call). The reference run uses a
/// target with no delay, so the numbers show what the delay adds.
/// </para>
/// </summary>
internal static class SlowTargetScenario
{
    private static readonly TimeSpan[] Delays =
    [
        TimeSpan.Zero,
        TimeSpan.FromMicroseconds(50),
        TimeSpan.FromMilliseconds(1),
    ];

    public static int Run(string[] args)
    {
        var seconds = ReadOption(args, "--seconds", 10);
        var runs = ReadOption(args, "--runs", 3);

        PrintEnvironment();

        // One short run first, so that JIT compilation and thread start-up do not distort the first real run.
        Execute(new SlowTargetConfig(
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromMilliseconds(300)));

        Console.WriteLine();
        Console.WriteLine("| Target delay | Run | Events | Triggers | Handover avg | Handover max "
            + "| Log p50 | Log p99 | Log p99.9 | Log max |");
        Console.WriteLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");

        foreach (var delay in Delays)
        {
            for (var run = 1; run <= runs; run++)
            {
                var config = new SlowTargetConfig(
                    delay,
                    TimeSpan.FromSeconds(seconds),
                    TimeSpan.FromMilliseconds(500),
                    TimeSpan.FromSeconds(1));
                PrintRow(config, run, Execute(config));
            }
        }

        Console.WriteLine();
        Console.WriteLine(
            "Times are in microseconds. Percentiles are rounded up to the bucket edge (at most 12.5 % high).");
        return 0;
    }

    public static SlowTargetResult Execute(SlowTargetConfig config)
    {
        var target = new SlowTargetSink(config.TargetDelay);
        using var logger = BenchmarkLoggers.CreateBuffered(ContextProfile.None, target, config.BufferSize);

        var intervalTicks = Stopwatch.Frequency / config.EventsPerSecondPerThread;
        var start = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 10; // all threads wait for the same start
        var end = start + ToTicks(config.Duration);

        var histograms = new LatencyHistogram[config.LogThreads];
        var counts = new long[config.LogThreads];
        var threads = new List<Thread>();

        for (var i = 0; i < config.LogThreads; i++)
        {
            var index = i;
            var histogram = histograms[index] = new LatencyHistogram();
            threads.Add(new Thread(() => counts[index] = LogLoop(logger, histogram, start, end, intervalTicks)));
        }

        var handover = new LatencyHistogram();
        var triggers = 0;
        threads.Add(new Thread(() => triggers = TriggerLoop(logger, handover, start, end, config)));

        foreach (var thread in threads)
        {
            thread.Start();
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }

        var latency = new LatencyHistogram();
        foreach (var histogram in histograms)
        {
            latency.Add(histogram);
        }

        return new SlowTargetResult(latency, handover, counts.Sum(), triggers, target.EmittedCount);
    }

    private static long LogLoop(
        Logger logger,
        LatencyHistogram histogram,
        long start,
        long end,
        long intervalTicks)
    {
        long count = 0;
        var due = start;
        while (due < end)
        {
            SpinWaiter.SpinUntil(due);
            logger.Debug(BenchmarkLoggers.Template, 42, "Contoso", 17);
            histogram.Record(ToNanoseconds(Stopwatch.GetTimestamp() - due));
            count++;
            due += intervalTicks;
        }

        return count;
    }

    private static int TriggerLoop(
        Logger logger,
        LatencyHistogram handover,
        long start,
        long end,
        SlowTargetConfig config)
    {
        var triggers = 0;
        var due = start + ToTicks(config.FirstTrigger);
        var intervalTicks = ToTicks(config.TriggerInterval);
        while (due < end)
        {
            SpinWaiter.SpinUntil(due);
            var before = Stopwatch.GetTimestamp();
            logger.Error(BenchmarkLoggers.Template, 42, "Contoso", 17);
            handover.Record(ToNanoseconds(Stopwatch.GetTimestamp() - before));
            triggers++;
            due += intervalTicks;
        }

        return triggers;
    }

    private static void PrintRow(SlowTargetConfig config, int run, SlowTargetResult result)
    {
        var delay = config.TargetDelay == TimeSpan.Zero
            ? "0 (reference)"
            : $"{config.TargetDelay.TotalMicroseconds:0} µs";
        var handover = result.HandoverTime;
        var latency = result.LogLatency;
        Console.WriteLine(
            $"| {delay} | {run} | {result.EventsLogged} | {result.Triggers} "
            + $"| {Microseconds(handover.MeanNanoseconds)} | {Microseconds(handover.MaxNanoseconds)} "
            + $"| {Microseconds(latency.Percentile(50))} | {Microseconds(latency.Percentile(99))} "
            + $"| {Microseconds(latency.Percentile(99.9))} | {Microseconds(latency.MaxNanoseconds)} |");
    }

    private static void PrintEnvironment()
    {
        Console.WriteLine($"CPU cores: {Environment.ProcessorCount}");
        Console.WriteLine($"OS: {RuntimeInformation.OSDescription}");
        Console.WriteLine($".NET: {RuntimeInformation.FrameworkDescription}");
        Console.WriteLine("Scenario D (slow target)");

        // The 4 logging threads and the trigger thread (which spins inside the target) each keep a core busy.
        if (Environment.ProcessorCount < 6)
        {
            Console.WriteLine(
                "WARNING: fewer than 6 cores. The threads compete for cores, so the latencies below are distorted.");
        }
    }

    private static string Microseconds(double nanoseconds)
        => (nanoseconds / 1000.0).ToString("0.0", CultureInfo.InvariantCulture);

    private static long ToTicks(TimeSpan time) => (long)(time.TotalSeconds * Stopwatch.Frequency);

    private static long ToNanoseconds(long ticks) => (long)(ticks * (1_000_000_000.0 / Stopwatch.Frequency));

    private static int ReadOption(string[] args, string name, int defaultValue)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out var value)
            ? value
            : defaultValue;
    }
}
