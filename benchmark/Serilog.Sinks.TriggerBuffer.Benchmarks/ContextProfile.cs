namespace Serilog.Sinks.TriggerBuffer.Benchmarks;

/// <summary>
/// How much analysis metadata every event carries. The profiles mix integers, strings, byte arrays and dictionaries
/// with fixed values, so that runs are reproducible.
/// </summary>
public enum ContextProfile
{
    None,
    Small,
    Medium,
    Large,
}

/// <summary>The exact composition of one <see cref="ContextProfile"/>.</summary>
internal sealed record ProfileShape(
    int Integers,
    int Strings,
    int[] ByteArrayLengths,
    int Dictionaries,
    int DictionaryEntries)
{
    public int PropertyCount => Integers + Strings + ByteArrayLengths.Length + Dictionaries;

    public static ProfileShape For(ContextProfile profile) => profile switch
    {
        ContextProfile.None => new ProfileShape(0, 0, [], 0, 0),
        ContextProfile.Small => new ProfileShape(2, 2, [16], 1, 3),
        ContextProfile.Medium => new ProfileShape(8, 8, [16, 256], 2, 10),
        ContextProfile.Large => new ProfileShape(20, 20, [16, 32, 64, 128, 256], 5, 20),
        _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, null),
    };
}
