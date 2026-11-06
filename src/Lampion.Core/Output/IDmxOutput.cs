using Lampion.Core.Engine;

namespace Lampion.Core.Output;

/// <summary>Somewhere DMX frames go: a network protocol or a USB interface. Not thread-safe; call from one thread.</summary>
public interface IDmxOutput : IDisposable
{
    /// <summary>Sends the frame; may block for a frame's transmit time on serial devices. Allocation-free.</summary>
    /// <exception cref="IOException">A USB device went away or stopped taking data.</exception>
    /// <exception cref="System.Net.Sockets.SocketException">The network refused a packet.</exception>
    void Send(DmxFrame frame);
}
