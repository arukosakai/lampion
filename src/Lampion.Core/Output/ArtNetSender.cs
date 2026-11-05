using System.Net;
using System.Net.Sockets;
using Lampion.Core.Engine;

namespace Lampion.Core.Output;

/// <summary>Sends frames as Art-Net ArtDmx packets, so a node or a running QLC+ can show the output live.</summary>
public sealed class ArtNetSender : IDisposable
{
    public const int Port = 6454;

    private readonly UdpClient _client;
    private readonly IPEndPoint _target;
    private byte _sequence;

    public ArtNetSender(string host = "255.255.255.255")
    {
        _client = new UdpClient { EnableBroadcast = true };
        _target = new IPEndPoint(IPAddress.Parse(host), Port);
    }

    /// <summary>Lampion universe 1 is Art-Net port-address 0.</summary>
    public void Send(DmxFrame frame)
    {
        _sequence = (byte)(_sequence == 255 ? 1 : _sequence + 1);
        foreach (var (universe, data) in frame.Universes)
        {
            var packet = BuildPacket(universe - 1, data, _sequence);
            _client.Send(packet, packet.Length, _target);
        }
    }

    internal static byte[] BuildPacket(int portAddress, ReadOnlySpan<byte> data, byte sequence)
    {
        var packet = new byte[18 + 512];
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
        data[..Math.Min(512, data.Length)].CopyTo(packet.AsSpan(18));
        return packet;
    }

    public void Dispose() => _client.Dispose();
}
