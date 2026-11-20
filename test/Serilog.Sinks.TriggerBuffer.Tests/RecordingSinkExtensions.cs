using System.Collections.Concurrent;
using Serilog.Configuration;

namespace Serilog.Sinks.TriggerBuffer.Tests;

/// <summary>
/// A target sink that can be named in JSON configuration. Serilog.Settings.Configuration can only create sinks
/// through public extension methods, so the recorded events are looked up by name.
/// </summary>
public static class RecordingSinkExtensions
{
    private static readonly ConcurrentDictionary<string, CollectingSink> Sinks = new();

    public static LoggerConfiguration Recording(this LoggerSinkConfiguration loggerSinkConfiguration, string name)
    {
        return loggerSinkConfiguration.Sink(Get(name));
    }

    internal static CollectingSink Get(string name) => Sinks.GetOrAdd(name, _ => new CollectingSink());
}
