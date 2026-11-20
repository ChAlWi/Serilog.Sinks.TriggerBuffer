using System;
using Serilog.Configuration;
using Serilog.Events;
using Serilog.Sinks.TriggerBuffer;

namespace Serilog;

public static class TriggerBufferLoggerConfigurationExtensions
{
    public static LoggerConfiguration TriggerBuffer(
        this LoggerSinkConfiguration loggerSinkConfiguration,
        Action<LoggerSinkConfiguration> configureTarget,
        LogEventLevel bufferMinimumLevel = LogEventLevel.Verbose,
        LogEventLevel triggerLevel = LogEventLevel.Error)
    {
        ArgumentNullException.ThrowIfNull(loggerSinkConfiguration);
        ArgumentNullException.ThrowIfNull(configureTarget);

        var sink = LoggerSinkConfiguration.Wrap(
            target => new TriggerBufferSink(target, bufferMinimumLevel, triggerLevel),
            configureTarget);
        return loggerSinkConfiguration.Sink(sink);
    }
}
