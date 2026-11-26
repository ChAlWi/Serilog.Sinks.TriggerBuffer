using System.Diagnostics;
using Serilog.Events;

namespace Serilog.Sinks.TriggerBuffer.Benchmarks;

/// <summary>
/// The <c>--verify</c> mode. It checks, through the public Serilog API, that the benchmarks measure what they claim
/// to measure, before their numbers are trusted. It does not measure anything.
/// </summary>
internal static class Verifier
{
    private static readonly string[] TemplateProperties = ["OrderId", "Customer", "Elapsed"];

    public static int Run()
    {
        var failures = new List<string>();

        CheckTriggerForwardsContextInOrder(failures);
        CheckDisplacementKeepsNewestEvents(failures);
        foreach (var profile in Enum.GetValues<ContextProfile>())
        {
            CheckProfile(profile, failures);
        }

        CheckControlRetainsOnlyTheLastEvents(failures);
        CheckSlowTargetWaitsAtLeastItsDelay(failures);
        CheckHistogramPercentilesAndMaximum(failures);
        CheckSlowTargetScenarioRunsEndToEnd(failures);

        if (failures.Count == 0)
        {
            Console.WriteLine("Verify: all checks passed.");
            return 0;
        }

        foreach (var failure in failures)
        {
            Console.Error.WriteLine($"Verify failed: {failure}");
        }

        return 1;
    }

    private static void CheckTriggerForwardsContextInOrder(List<string> failures)
    {
        var target = new RecordingSink();
        using (var logger = BenchmarkLoggers.CreateBuffered(ContextProfile.None, target, 1000))
        {
            for (var i = 0; i < 5; i++)
            {
                logger.Debug(BenchmarkLoggers.Template, i, "Contoso", 17);
            }

            if (target.Events.Count != 0)
            {
                failures.Add($"events below the trigger reached the target ({target.Events.Count} events)");
            }

            logger.Error(BenchmarkLoggers.Template, 99, "Contoso", 17);
        }

        CheckOrderIds(target, [0, 1, 2, 3, 4, 99], "a trigger forwards the context, then itself", failures);
    }

    private static void CheckDisplacementKeepsNewestEvents(List<string> failures)
    {
        var target = new RecordingSink();
        using (var logger = BenchmarkLoggers.CreateBuffered(ContextProfile.None, target, 3))
        {
            for (var i = 0; i < 5; i++)
            {
                logger.Debug(BenchmarkLoggers.Template, i, "Contoso", 17);
            }

            logger.Error(BenchmarkLoggers.Template, 99, "Contoso", 17);
        }

        CheckOrderIds(target, [2, 3, 4, 99], "a full buffer keeps the newest events", failures);
    }

    private static void CheckControlRetainsOnlyTheLastEvents(List<string> failures)
    {
        var target = new RetainingSink(3);
        using (var logger = BenchmarkLoggers.CreatePlain(ContextProfile.None, target))
        {
            for (var i = 0; i < 5; i++)
            {
                logger.Debug(BenchmarkLoggers.Template, i, "Contoso", 17);
            }
        }

        var retained = target.Retained()
            .Select(e => e.Properties["OrderId"] is ScalarValue { Value: int id } ? id : -1)
            .Order()
            .ToArray();

        if (!retained.SequenceEqual([2, 3, 4]))
        {
            failures.Add($"control: expected the last 3 events [2, 3, 4], got [{string.Join(", ", retained)}]");
        }
    }

    private static void CheckSlowTargetWaitsAtLeastItsDelay(List<string> failures)
    {
        const int events = 20;
        var delay = TimeSpan.FromMicroseconds(200);
        var target = new SlowTargetSink(delay);
        using var logger = BenchmarkLoggers.CreatePlain(ContextProfile.None, target);

        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < events; i++)
        {
            logger.Debug(BenchmarkLoggers.Template, i, "Contoso", 17);
        }

        stopwatch.Stop();

        if (target.EmittedCount != events)
        {
            failures.Add($"slow target: expected {events} events, got {target.EmittedCount}");
        }

        if (stopwatch.Elapsed < delay * events)
        {
            failures.Add($"slow target: {events} events took {stopwatch.Elapsed.TotalMilliseconds:0.00} ms, "
                + $"less than the configured {(delay * events).TotalMilliseconds:0.00} ms");
        }
    }

    private static void CheckHistogramPercentilesAndMaximum(List<string> failures)
    {
        var histogram = new LatencyHistogram();
        for (var i = 0; i < 100; i++)
        {
            histogram.Record(1000);
        }

        histogram.Record(5_000_000);

        var median = histogram.Percentile(50);
        if (median is < 1000 or > 1125)
        {
            failures.Add($"histogram: the median of 100 x 1000 ns and one outlier is {median} ns, expected 1000-1125");
        }

        if (histogram.MaxNanoseconds != 5_000_000 || histogram.Percentile(100) != 5_000_000)
        {
            failures.Add("histogram: the maximum is not exact");
        }

        if (histogram.Count != 101)
        {
            failures.Add($"histogram: expected 101 samples, got {histogram.Count}");
        }
    }

    private static void CheckSlowTargetScenarioRunsEndToEnd(List<string> failures)
    {
        var config = new SlowTargetConfig(
            TimeSpan.FromMicroseconds(20),
            TimeSpan.FromMilliseconds(600),
            TimeSpan.FromMilliseconds(150),
            TimeSpan.FromMilliseconds(200),
            LogThreads: 2,
            EventsPerSecondPerThread: 2000,
            BufferSize: 50);

        var result = SlowTargetScenario.Execute(config);

        if (result.Triggers < 1)
        {
            failures.Add("scenario D: no trigger was sent");
        }

        if (result.LogLatency.Count != result.EventsLogged || result.EventsLogged < 1)
        {
            failures.Add(
                $"scenario D: {result.EventsLogged} events logged, {result.LogLatency.Count} latencies recorded");
        }

        // Every trigger hands over its own event, so the target has to have received at least one event per trigger.
        if (result.TargetEvents < result.Triggers)
        {
            failures.Add($"scenario D: {result.Triggers} triggers but only {result.TargetEvents} events at the target");
        }
    }

    private static void CheckOrderIds(RecordingSink target, int[] expected, string check, List<string> failures)
    {
        var actual = target.Events
            .Select(e => e.Properties["OrderId"] is ScalarValue { Value: int id } ? id : -1)
            .ToArray();

        if (!actual.SequenceEqual(expected))
        {
            failures.Add($"{check}: expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}]");
        }
    }

    private static void CheckProfile(ContextProfile profile, List<string> failures)
    {
        var shape = ProfileShape.For(profile);
        var target = new RecordingSink();
        using (var logger = BenchmarkLoggers.CreateBuffered(profile, target, 1000))
        {
            logger.Error(BenchmarkLoggers.Template, 1, "Contoso", 17);
        }

        if (target.Events.Count != 1)
        {
            failures.Add($"profile {profile}: expected 1 event at the target, got {target.Events.Count}");
            return;
        }

        var context = target.Events[0].Properties
            .Where(p => !TemplateProperties.Contains(p.Key))
            .ToList();

        Expect(profile, "properties", shape.PropertyCount, context.Count, failures);
        var integers = context.Count(p => p.Value is ScalarValue { Value: int });
        Expect(profile, "integers", shape.Integers, integers, failures);
        Expect(profile, "strings", shape.Strings, CountStrings(context), failures);
        Expect(profile, "byte arrays", shape.ByteArrayLengths.Length, CountByteArrays(context, shape), failures);
        Expect(profile, "dictionaries", shape.Dictionaries, CountDictionaries(context, shape), failures);
    }

    // Serilog does not keep a byte array as an array: it becomes a scalar value that holds the bytes as a hex string.
    // That is what the sink receives, so the profiles are checked against that representation. The byte arrays are
    // found by name, because their hex strings are strings like the others.
    private static int CountStrings(List<KeyValuePair<string, LogEventPropertyValue>> context)
    {
        return context.Count(p => p.Key.StartsWith("Str", StringComparison.Ordinal)
            && p.Value is ScalarValue { Value: string });
    }

    private static int CountByteArrays(List<KeyValuePair<string, LogEventPropertyValue>> context, ProfileShape shape)
    {
        var count = 0;
        for (var i = 0; i < shape.ByteArrayLengths.Length; i++)
        {
            var found = context.FirstOrDefault(p => p.Key == $"Bytes{i}").Value;
            var expected = Convert.ToHexString(Enumerable.Repeat((byte)i, shape.ByteArrayLengths[i]).ToArray());
            if (found is ScalarValue { Value: string hex }
                && string.Equals(hex, expected, StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }

        return count;
    }

    private static int CountDictionaries(List<KeyValuePair<string, LogEventPropertyValue>> context, ProfileShape shape)
    {
        return context.Count(p => p.Value is DictionaryValue dictionary
            && dictionary.Elements.Count == shape.DictionaryEntries);
    }

    private static void Expect(ContextProfile profile, string what, int expected, int actual, List<string> failures)
    {
        if (expected != actual)
        {
            failures.Add($"profile {profile}: expected {expected} {what}, got {actual}");
        }
    }
}
