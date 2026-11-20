using Serilog.Events;
using Shouldly;
using Xunit;

namespace Serilog.Sinks.TriggerBuffer.Tests;

public class TriggerBufferTests
{
    [Fact]
    public void Events_below_the_trigger_level_are_not_forwarded()
    {
        var target = new CollectingSink();
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.TriggerBuffer(t => t.Sink(target))
            .CreateLogger();

        logger.Verbose("verbose");
        logger.Debug("debug");
        logger.Information("information");
        logger.Warning("warning");

        target.Events.ShouldBeEmpty();
    }

    [Fact]
    public void An_error_forwards_the_buffered_context_in_order_followed_by_the_error()
    {
        var target = new CollectingSink();
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.TriggerBuffer(t => t.Sink(target))
            .CreateLogger();

        logger.Information("context 1");
        logger.Debug("context 2");
        logger.Warning("context 3");
        logger.Error("trigger");

        target.Messages.ShouldBe(["context 1", "context 2", "context 3", "trigger"]);
    }

    [Fact]
    public void A_fatal_event_triggers_like_an_error()
    {
        var target = new CollectingSink();
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.TriggerBuffer(t => t.Sink(target))
            .CreateLogger();

        logger.Information("context");
        logger.Fatal("trigger");

        target.Messages.ShouldBe(["context", "trigger"]);
    }

    [Fact]
    public void A_trigger_without_context_is_forwarded_exactly_once()
    {
        var target = new CollectingSink();
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.TriggerBuffer(t => t.Sink(target))
            .CreateLogger();

        logger.Error("trigger");

        target.Messages.ShouldBe(["trigger"]);
    }

    [Fact]
    public void A_lower_trigger_level_triggers_at_that_level()
    {
        var target = new CollectingSink();
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.TriggerBuffer(t => t.Sink(target), triggerLevel: LogEventLevel.Warning)
            .CreateLogger();

        logger.Information("context");
        logger.Warning("trigger");

        target.Messages.ShouldBe(["context", "trigger"]);
    }

    [Fact]
    public void A_higher_trigger_level_does_not_trigger_on_an_error()
    {
        var target = new CollectingSink();
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.TriggerBuffer(t => t.Sink(target), triggerLevel: LogEventLevel.Fatal)
            .CreateLogger();

        logger.Information("context");
        logger.Error("error");
        target.Events.ShouldBeEmpty();

        logger.Fatal("trigger");

        target.Messages.ShouldBe(["context", "error", "trigger"]);
    }

    [Fact]
    public void Events_below_the_buffer_minimum_level_are_neither_buffered_nor_forwarded()
    {
        var target = new CollectingSink();
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.TriggerBuffer(t => t.Sink(target), bufferMinimumLevel: LogEventLevel.Information)
            .CreateLogger();

        logger.Verbose("dropped verbose");
        logger.Debug("dropped debug");
        logger.Information("kept");
        logger.Error("trigger");

        target.Messages.ShouldBe(["kept", "trigger"]);
    }

    [Fact]
    public void Only_the_newest_context_events_within_the_maximum_count_are_forwarded()
    {
        var target = new CollectingSink();
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.TriggerBuffer(t => t.Sink(target), maximumBufferCount: 3)
            .CreateLogger();

        for (var i = 1; i <= 5; i++)
        {
            logger.Information("context {Number}", i);
        }

        logger.Error("trigger");

        target.Messages.ShouldBe(["context 3", "context 4", "context 5", "trigger"]);
    }

    [Fact]
    public void Context_older_than_the_maximum_age_is_not_forwarded()
    {
        var target = new CollectingSink();
        var clock = new ManualTimeProvider();
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.TriggerBuffer(t => t.Sink(target), clock, maximumBufferAge: TimeSpan.FromSeconds(10))
            .CreateLogger();

        logger.Information("old");
        clock.Advance(TimeSpan.FromSeconds(11));
        logger.Information("recent");
        clock.Advance(TimeSpan.FromSeconds(5));
        logger.Error("trigger");

        target.Messages.ShouldBe(["recent", "trigger"]);
    }

    [Fact]
    public void Count_and_age_limits_apply_at_the_same_time()
    {
        var target = new CollectingSink();
        var clock = new ManualTimeProvider();
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.TriggerBuffer(
                t => t.Sink(target),
                clock,
                maximumBufferCount: 3,
                maximumBufferAge: TimeSpan.FromSeconds(10))
            .CreateLogger();

        logger.Information("expired 1");
        logger.Information("expired 2");
        clock.Advance(TimeSpan.FromSeconds(11));
        logger.Information("displaced");
        logger.Information("kept 1");
        logger.Information("kept 2");
        logger.Information("kept 3");
        logger.Error("trigger");

        target.Messages.ShouldBe(["kept 1", "kept 2", "kept 3", "trigger"]);
    }

    [Fact]
    public void Two_consecutive_triggers_are_both_forwarded_and_context_appears_only_once()
    {
        var target = new CollectingSink();
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.TriggerBuffer(t => t.Sink(target))
            .CreateLogger();

        logger.Information("context");
        logger.Error("first trigger");
        logger.Error("second trigger");

        target.Messages.ShouldBe(["context", "first trigger", "second trigger"]);
    }

    [Fact]
    public void A_failing_target_does_not_stop_the_remaining_events_and_is_reported_through_selflog()
    {
        var target = new FailingSink(message => message == "context 2");
        using var selfLog = new SelfLogCapture();
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.TriggerBuffer(t => t.Sink(target))
            .CreateLogger();

        logger.Information("context 1");
        logger.Information("context 2");
        logger.Information("context 3");
        Should.NotThrow(() => logger.Error("trigger"));

        target.Messages.ShouldBe(["context 1", "context 3", "trigger"]);
        selfLog.Messages.ShouldContain(message => message.Contains("TriggerBuffer"));
    }

    [Fact]
    public void A_failing_trigger_is_not_retried_and_the_taken_context_is_not_forwarded_again()
    {
        var target = new FailingSink(message => message == "first trigger");
        using var selfLog = new SelfLogCapture();
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.TriggerBuffer(t => t.Sink(target))
            .CreateLogger();

        logger.Information("context");
        Should.NotThrow(() => logger.Error("first trigger"));
        logger.Error("second trigger");

        target.Messages.ShouldBe(["context", "second trigger"]);
        selfLog.Messages.ShouldContain(message => message.Contains("TriggerBuffer"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_non_positive_maximum_count_is_rejected(int maximumBufferCount)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new LoggerConfiguration()
            .WriteTo.TriggerBuffer(t => t.Sink(new CollectingSink()), maximumBufferCount: maximumBufferCount));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_non_positive_maximum_age_is_rejected(int seconds)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new LoggerConfiguration()
            .WriteTo.TriggerBuffer(t => t.Sink(new CollectingSink()), maximumBufferAge: TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void A_buffer_minimum_level_above_the_trigger_level_is_rejected()
    {
        Should.Throw<ArgumentException>(() => new LoggerConfiguration()
            .WriteTo.TriggerBuffer(
                t => t.Sink(new CollectingSink()),
                bufferMinimumLevel: LogEventLevel.Fatal,
                triggerLevel: LogEventLevel.Error));
    }

    [Fact]
    public void Equal_buffer_minimum_and_trigger_levels_are_allowed_and_leave_no_context_range()
    {
        var target = new CollectingSink();
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.TriggerBuffer(
                t => t.Sink(target),
                bufferMinimumLevel: LogEventLevel.Error,
                triggerLevel: LogEventLevel.Error)
            .CreateLogger();

        logger.Warning("dropped");
        logger.Error("trigger");

        target.Messages.ShouldBe(["trigger"]);
    }

    [Fact]
    public async Task A_blocked_target_makes_the_trigger_wait_and_blocks_never_interleave()
    {
        var target = new BlockingSink(blockOnMessage: "context 1");
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.TriggerBuffer(t => t.Sink(target))
            .CreateLogger();
        logger.Information("context 1");
        logger.Information("context 2");

        var cancellation = TestContext.Current.CancellationToken;
        var first = Task.Run(() => logger.Error("trigger A"), cancellation);
        target.Entered.Wait(TimeSpan.FromSeconds(10), cancellation).ShouldBeTrue();

        var second = Task.Run(
            () =>
            {
                logger.Information("context B");
                logger.Error("trigger B");
            },
            cancellation);

        // Give the second caller the chance to overlap with the block that is being handed over.
        await Task.WhenAny(second, Task.Delay(TimeSpan.FromMilliseconds(300), cancellation));
        first.IsCompleted.ShouldBeFalse("the triggering call returns only after the handover");

        target.Release.Set();
        await Task.WhenAll(first, second);

        target.Messages.ShouldBe(["context 1", "context 2", "trigger A", "context B", "trigger B"]);
    }

    [Fact]
    public async Task Parallel_intake_and_triggers_neither_duplicate_nor_lose_events()
    {
        const int threadCount = 8;
        const int eventsPerThread = 500;
        const int triggerEvery = 25;

        var target = new CollectingSink();
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.TriggerBuffer(t => t.Sink(target), maximumBufferCount: 100_000)
            .CreateLogger();

        // All threads wait on one gate, so that they start logging at the same moment.
        using var startGate = new ManualResetEventSlim(false);
        var workers = Enumerable.Range(0, threadCount).Select(thread => Task.Factory.StartNew(
            () =>
            {
                startGate.Wait(TestContext.Current.CancellationToken);
                for (var i = 0; i < eventsPerThread; i++)
                {
                    if (i % triggerEvery == triggerEvery - 1)
                    {
                        logger.Error("{Thread}-{Index}", thread, i);
                    }
                    else
                    {
                        logger.Information("{Thread}-{Index}", thread, i);
                    }
                }
            },
            TestContext.Current.CancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default)).ToArray();

        startGate.Set();
        await Task.WhenAll(workers);
        logger.Error("final trigger");

        var delivered = target.Events;
        var messages = delivered.Select(e => e.RenderMessage()).ToArray();

        // Every event arrives exactly once: the context of the final trigger completes the last open block.
        messages.Length.ShouldBe((threadCount * eventsPerThread) + 1);
        messages.Distinct().Count().ShouldBe(messages.Length);

        // Events of one thread keep their order, and every block ends with its trigger event.
        for (var thread = 0; thread < threadCount; thread++)
        {
            var indexes = messages
                .Where(m => m.StartsWith($"{thread}-", StringComparison.Ordinal))
                .Select(m => int.Parse(m[(m.IndexOf('-') + 1)..], System.Globalization.CultureInfo.InvariantCulture))
                .ToArray();
            indexes.ShouldBe(indexes.Order().ToArray());
        }

        delivered[^1].Level.ShouldBe(LogEventLevel.Error);
        delivered.Count(e => e.Level == LogEventLevel.Error)
            .ShouldBe((threadCount * (eventsPerThread / triggerEvery)) + 1);
    }

    [Fact]
    public void An_event_logged_back_into_the_same_sink_from_the_target_is_ignored_and_reported()
    {
        var target = new ReentrantSink();
        using var selfLog = new SelfLogCapture();
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.TriggerBuffer(t => t.Sink(target))
            .CreateLogger();
        target.Logger = logger;

        logger.Information("context");
        logger.Error("trigger");
        logger.Error("second trigger");

        target.Messages.ShouldBe(["context", "trigger", "second trigger"]);
        selfLog.Messages.ShouldContain(message => message.Contains("TriggerBuffer"));
    }

    [Fact]
    public void A_missing_target_configuration_is_rejected()
    {
        // The null literal is intentional: it is the invalid input under test.
        Should.Throw<ArgumentNullException>(() => new LoggerConfiguration().WriteTo.TriggerBuffer(null!));
    }
}
