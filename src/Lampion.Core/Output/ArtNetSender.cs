using System.Net;
using System.Net.Sockets;
using Lampion.Core.Engine;

namespace Lampion.Core.Output;

/// <summary>
/// Sends frames as Art-Net ArtDmx packets over UDP, so a node or a running QLC+ can show the output live.
/// Reuses one packet buffer and a pre-resolved address, so sending allocates nothing. Not thread-safe.
/// </summary>
public sealed class ArtNetSender : IDisposable
{
    /// <summary>The Art-Net UDP port.</summary>
    public const int Port = 6454;

    private readonly UdpClient _client;
    private readonly SocketAddress _target;
    private readonly byte[] _packet = new byte[ArtDmxPacket.Length];
    private byte _sequence;

    /// <summary>Opens a broadcast-capable UDP socket aimed at <paramref name="host"/>.</summary>
    /// <param name="host">IPv4 address to send to; the limited broadcast address by default.</param>
    /// <exception cref="FormatException"><paramref name="host"/> is not an IP address.</exception>
    /// <exception cref="SocketException">The socket could not be opened.</exception>
    public ArtNetSender(string host = "255.255.255.255")
    {
        _client = new UdpClient { EnableBroadcast = true };
        _target = new IPEndPoint(IPAddress.Parse(host), Port).Serialize();
    }

    /// <summary>Sends every universe in the frame, in ascending order. Lampion universe 1 is Art-Net port-address 0.</summary>
    /// <exception cref="SocketException">The network refused a packet.</exception>
    public void Send(DmxFrame frame)
    {
        _sequence = (byte)(_sequence == 255 ? 1 : _sequence + 1);
        foreach (var (universe, data) in frame)
        {
            ArtDmxPacket.Write(_packet, universe - 1, data, _sequence);
            _client.Client.SendTo(_packet, SocketFlags.None, _target);
        }
    }

    /// <summary>Closes the socket.</summary>
    public void Dispose() => _client.Dispose();
}
