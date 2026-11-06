using System.Diagnostics;
using System.IO.Ports;
using Lampion.Core.Engine;

namespace Lampion.Core.Output;

/// <summary>
/// Sends one universe through an Enttec Open DMX USB or another plain FTDI cable, which has no processor of its own:
/// the computer drives the DMX line directly at 250 kbaud, so every frame is a break followed by 513 bytes.
/// </summary>
public sealed class OpenDmxSender : IDmxOutput
{
    // Start code plus 512 channels at 11 bits each (start, 8 data, 2 stop) on a 250 kbaud line.
    private static readonly TimeSpan TransmitTime = TimeSpan.FromMilliseconds(513 * 11 / 250.0 + 1);

    private readonly SerialPort _port;
    private readonly int _universe;
    private readonly byte[] _packet = new byte[1 + 512];
    private long _lastWrite;

    /// <summary>Opens <paramref name="portName"/> to send Lampion universe <paramref name="universe"/>.</summary>
    /// <exception cref="IOException">The port doesn't exist or is in use.</exception>
    /// <exception cref="UnauthorizedAccessException">Another program holds the port.</exception>
    public OpenDmxSender(string portName, int universe)
    {
        _universe = universe;
        _port = new SerialPort(portName, 250000, Parity.None, 8, StopBits.Two) { WriteTimeout = 500 };
        _port.Open();
    }

    /// <inheritdoc/>
    /// <remarks>Blocks until the previous frame has left the cable, then sleeps through the break.</remarks>
    public void Send(DmxFrame frame)
    {
        frame.TryGetUniverse(_universe, out var data);
        _packet[0] = 0;
        var count = Math.Min(512, data.Length);
        data[..count].CopyTo(_packet.AsSpan(1));
        _packet.AsSpan(1 + count).Clear();

        // A break sent while the last frame is still in the chip's buffer would cut it short.
        var remaining = TransmitTime - Stopwatch.GetElapsedTime(_lastWrite);
        if (_lastWrite != 0 && remaining > TimeSpan.Zero)
            Thread.Sleep(remaining);
        try
        {
            // DMX needs a break of at least 88 µs; a 1 ms sleep is the shortest the OS reliably gives.
            _port.BreakState = true;
            Thread.Sleep(1);
            _port.BreakState = false;
            _port.Write(_packet, 0, _packet.Length);
        }
        catch (TimeoutException ex)
        {
            throw new IOException("The cable stopped taking data.", ex);
        }
        _lastWrite = Stopwatch.GetTimestamp();
    }

    /// <summary>Closes the port.</summary>
    public void Dispose() => _port.Dispose();
}
