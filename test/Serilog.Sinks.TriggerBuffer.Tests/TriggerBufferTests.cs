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
    public void A_missing_target_configuration_is_rejected()
    {
        // The null literal is intentional: it is the invalid input under test.
        Should.Throw<ArgumentNullException>(() => new LoggerConfiguration().WriteTo.TriggerBuffer(null!));
    }
}
