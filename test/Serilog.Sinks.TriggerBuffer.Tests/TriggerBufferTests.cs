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
}
