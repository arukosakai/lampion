namespace Lampion.Core.Engine;

/// <summary>One universe of a <see cref="DmxFrame"/>, as seen while enumerating it. Only valid until the frame changes.</summary>
public readonly ref struct DmxUniverse
{
    /// <summary>A view of one universe.</summary>
    public DmxUniverse(int number, ReadOnlySpan<byte> channels)
    {
        Number = number;
        Channels = channels;
    }

    /// <summary>1-based universe number.</summary>
    public int Number { get; }

    /// <summary>The 512 channels; index 0 is channel 1.</summary>
    public ReadOnlySpan<byte> Channels { get; }

    /// <summary>Supports <c>foreach (var (number, channels) in frame)</c>.</summary>
    public void Deconstruct(out int number, out ReadOnlySpan<byte> channels)
    {
        number = Number;
        channels = Channels;
    }
}
