using System.IO.Ports;
using Lampion.Core.Engine;

namespace Lampion.Core.Output;

/// <summary>
/// Sends one universe to an Enttec DMX USB Pro or a compatible interface (DMXking ultraDMX and others) through its
/// virtual serial port. The interface refreshes the DMX line itself, so a frame only has to be handed over.
/// </summary>
public sealed class EnttecProSender : IDmxOutput
{
    private readonly SerialPort _port;
    private readonly int _universe;
    private readonly byte[] _packet = new byte[EnttecProPacket.Length];

    /// <summary>Opens <paramref name="portName"/> to send Lampion universe <paramref name="universe"/>.</summary>
    /// <exception cref="IOException">The port doesn't exist or is in use.</exception>
    /// <exception cref="UnauthorizedAccessException">Another program holds the port.</exception>
    public EnttecProSender(string portName, int universe)
    {
        _universe = universe;
        // The baud rate is ignored by the USB chip but the driver insists on one.
        _port = new SerialPort(portName, 57600, Parity.None, 8, StopBits.One) { WriteTimeout = 500 };
        _port.Open();
    }

    /// <inheritdoc/>
    public void Send(DmxFrame frame)
    {
        frame.TryGetUniverse(_universe, out var data);
        EnttecProPacket.Write(_packet, data);
        try
        {
            _port.Write(_packet, 0, _packet.Length);
        }
        catch (TimeoutException ex)
        {
            throw new IOException("The interface stopped taking data.", ex);
        }
    }

    /// <summary>Closes the port.</summary>
    public void Dispose() => _port.Dispose();
}
