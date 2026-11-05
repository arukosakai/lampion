namespace Lampion.Core.Output;

/// <summary>Builds Art-Net 4 ArtDmx packets (OpDmx 0x5000, protocol 14) carrying a full 512-channel universe.</summary>
internal static class ArtDmxPacket
{
    /// <summary>Header plus 512 channels, in bytes.</summary>
    public const int Length = HeaderLength + 512;

    private const int HeaderLength = 18;

    /// <summary>Fills <paramref name="packet"/> (at least <see cref="Length"/> bytes) in place; allocation-free.</summary>
    /// <param name="packet">Destination; only the first <see cref="Length"/> bytes are written.</param>
    /// <param name="portAddress">15-bit Art-Net port-address (Net and SubUni); Lampion universe 1 is 0.</param>
    /// <param name="data">Channel values; anything past 512 is ignored and missing channels are sent as 0.</param>
    /// <param name="sequence">Sequence number, 1..255 (0 disables reordering at the receiver).</param>
    public static void Write(Span<byte> packet, int portAddress, ReadOnlySpan<byte> data, byte sequence)
    {
        "Art-Net\0"u8.CopyTo(packet);
        packet[8] = 0x00; // OpDmx, little-endian 0x5000
        packet[9] = 0x50;
        packet[10] = 0; // protocol version 14, big-endian
        packet[11] = 14;
        packet[12] = sequence;
        packet[13] = 0; // physical
        packet[14] = (byte)(portAddress & 0xFF); // SubUni
        packet[15] = (byte)((portAddress >> 8) & 0x7F); // Net
        packet[16] = 512 >> 8; // length, big-endian
        packet[17] = 512 & 0xFF;
        var channels = packet.Slice(HeaderLength, 512);
        var count = Math.Min(512, data.Length);
        data[..count].CopyTo(channels);
        channels[count..].Clear();
    }

    /// <summary>A new packet; see <see cref="Write"/>.</summary>
    public static byte[] Build(int portAddress, ReadOnlySpan<byte> data, byte sequence)
    {
        var packet = new byte[Length];
        Write(packet, portAddress, data, sequence);
        return packet;
    }
}
