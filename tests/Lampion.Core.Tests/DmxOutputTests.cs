using Lampion.Core.Output;

namespace Lampion.Core.Tests;

public class DmxOutputTests
{
    [Fact]
    public void EnttecProPacketFramesAFullUniverse()
    {
        var data = new byte[512];
        data[0] = 255;
        data[511] = 42;
        var packet = new byte[EnttecProPacket.Length];
        EnttecProPacket.Write(packet, data);

        Assert.Equal(518, packet.Length);
        Assert.Equal([0x7E, 6, 0x01, 0x02, 0], packet[..5]); // label 6, length 513 little-endian, start code 0
        Assert.Equal(255, packet[5]);
        Assert.Equal(42, packet[516]);
        Assert.Equal(0xE7, packet[^1]);
    }

    [Fact]
    public void EnttecProPacketZeroesMissingChannelsAndIgnoresExtras()
    {
        var packet = new byte[EnttecProPacket.Length];
        packet.AsSpan().Fill(0xAA);
        EnttecProPacket.Write(packet, [9, 8, 7]);
        Assert.Equal([9, 8, 7, 0], packet[5..9]);
        Assert.All(packet[8..^1], b => Assert.Equal(0, b));

        EnttecProPacket.Write(packet, new byte[600]);
        Assert.Equal(0xE7, packet[^1]);
    }

    [Fact]
    public void EnttecProPacketDoesNotAllocate()
    {
        var data = new byte[512];
        var packet = new byte[EnttecProPacket.Length];
        EnttecProPacket.Write(packet, data);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
            EnttecProPacket.Write(packet, data);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
