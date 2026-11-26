using Serilog.Core;
using Serilog.Events;

namespace Serilog.Sinks.TriggerBuffer.Benchmarks;

/// <summary>
/// Builds the loggers for all scenarios through the public configuration API, so that the benchmarks measure what
/// users experience.
/// </summary>
internal static class BenchmarkLoggers
{
    public const string Template = "Loaded {OrderId} for {Customer} in {Elapsed} ms";

    private const int MinimumStringLength = 20;
    private const int MaximumStringLength = 80;

    /// <summary>A logger that passes every event straight to <paramref name="target"/>.</summary>
    public static Logger CreatePlain(ContextProfile profile, ILogEventSink target)
    {
        var configuration = new LoggerConfiguration().MinimumLevel.Verbose();
        AddContext(configuration, profile);
        return configuration.WriteTo.Sink(target).CreateLogger();
    }

    /// <summary>A logger with the trigger buffer in front of <paramref name="target"/> and default levels.</summary>
    public static Logger CreateBuffered(ContextProfile profile, ILogEventSink target, int bufferSize)
    {
        var configuration = new LoggerConfiguration().MinimumLevel.Verbose();
        AddContext(configuration, profile);
        return configuration
            .WriteTo.TriggerBuffer(t => t.Sink(target), maximumBufferCount: bufferSize)
            .CreateLogger();
    }

    // Enrich.WithProperty is the common way to attach fixed metadata. Serilog converts the values for every event
    // (dictionaries are destructured each time). That work is part of the baseline and is not charged to the sink.
    private static void AddContext(LoggerConfiguration configuration, ContextProfile profile)
    {
        var shape = ProfileShape.For(profile);

        for (var i = 0; i < shape.Integers; i++)
        {
            configuration.Enrich.WithProperty($"Int{i}", 1000 + i);
        }

        for (var i = 0; i < shape.Strings; i++)
        {
            configuration.Enrich.WithProperty($"Str{i}", CreateString(i, shape.Strings));
        }

        for (var i = 0; i < shape.ByteArrayLengths.Length; i++)
        {
            configuration.Enrich.WithProperty($"Bytes{i}", CreateBytes(i, shape.ByteArrayLengths[i]));
        }

        for (var i = 0; i < shape.Dictionaries; i++)
        {
            configuration.Enrich.WithProperty($"Map{i}", CreateDictionary(i, shape.DictionaryEntries), true);
        }
    }

    // Lengths are spread over 20..80 characters, the same for every run.
    private static string CreateString(int index, int count)
    {
        var length = count == 1
            ? MinimumStringLength
            : MinimumStringLength + index * (MaximumStringLength - MinimumStringLength) / (count - 1);
        return new string((char)('a' + index % 26), length);
    }

    private static byte[] CreateBytes(int index, int length)
    {
        var bytes = new byte[length];
        Array.Fill(bytes, (byte)index);
        return bytes;
    }

    private static Dictionary<string, string> CreateDictionary(int index, int entries)
    {
        var dictionary = new Dictionary<string, string>(entries);
        for (var i = 0; i < entries; i++)
        {
            dictionary[$"key{i}"] = $"value{index}-{i}";
        }

        return dictionary;
    }
}
