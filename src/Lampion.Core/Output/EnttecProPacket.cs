namespace Lampion.Core.Output;

/// <summary>Builds Enttec DMX USB Pro "Output Only Send DMX" messages (label 6), which DMXking and other Pro clones also speak.</summary>
internal static class EnttecProPacket
{
    /// <summary>Start delimiter, label, two length bytes, start code plus 512 channels, end delimiter.</summary>
    public const int Length = 4 + 1 + 512 + 1;

    private const byte StartOfMessage = 0x7E;
    private const byte EndOfMessage = 0xE7;
    private const byte SendDmxLabel = 6;
    private const int PayloadLength = 1 + 512;

    /// <summary>Fills <paramref name="packet"/> (at least <see cref="Length"/> bytes) in place; allocation-free.</summary>
    /// <param name="packet">Destination; only the first <see cref="Length"/> bytes are written.</param>
    /// <param name="data">Channel values; anything past 512 is ignored and missing channels are sent as 0.</param>
    public static void Write(Span<byte> packet, ReadOnlySpan<byte> data)
    {
        packet[0] = StartOfMessage;
        packet[1] = SendDmxLabel;
        packet[2] = PayloadLength & 0xFF;
        packet[3] = PayloadLength >> 8;
        packet[4] = 0; // DMX start code
        var channels = packet.Slice(5, 512);
        var count = Math.Min(512, data.Length);
        data[..count].CopyTo(channels);
        channels[count..].Clear();
        packet[Length - 1] = EndOfMessage;
    }
}
