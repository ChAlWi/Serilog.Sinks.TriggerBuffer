using System;
using Serilog.Configuration;
using Serilog.Events;
using Serilog.Sinks.TriggerBuffer;

namespace Serilog;

public static class TriggerBufferLoggerConfigurationExtensions
{
    private static readonly TimeSpan DefaultMaximumBufferAge = TimeSpan.FromSeconds(60);

    public static LoggerConfiguration TriggerBuffer(
        this LoggerSinkConfiguration loggerSinkConfiguration,
        Action<LoggerSinkConfiguration> configureTarget,
        LogEventLevel bufferMinimumLevel = LogEventLevel.Verbose,
        LogEventLevel triggerLevel = LogEventLevel.Error,
        int maximumBufferCount = 1000,
        TimeSpan? maximumBufferAge = null)
    {
        return loggerSinkConfiguration.TriggerBuffer(
            configureTarget,
            TimeProvider.System,
            bufferMinimumLevel,
            triggerLevel,
            maximumBufferCount,
            maximumBufferAge);
    }

    internal static LoggerConfiguration TriggerBuffer(
        this LoggerSinkConfiguration loggerSinkConfiguration,
        Action<LoggerSinkConfiguration> configureTarget,
        TimeProvider timeProvider,
        LogEventLevel bufferMinimumLevel = LogEventLevel.Verbose,
        LogEventLevel triggerLevel = LogEventLevel.Error,
        int maximumBufferCount = 1000,
        TimeSpan? maximumBufferAge = null)
    {
        ArgumentNullException.ThrowIfNull(loggerSinkConfiguration);
        ArgumentNullException.ThrowIfNull(configureTarget);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var age = maximumBufferAge ?? DefaultMaximumBufferAge;
        var sink = LoggerSinkConfiguration.Wrap(
            target => new TriggerBufferSink(target, timeProvider, bufferMinimumLevel, triggerLevel, maximumBufferCount, age),
            configureTarget);
        return loggerSinkConfiguration.Sink(sink);
    }
}
