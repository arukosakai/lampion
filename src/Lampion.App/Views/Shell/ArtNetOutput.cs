using System.Diagnostics;
using System.Net.Sockets;
using Lampion.App.Editor;
using Lampion.Core.Output;

namespace Lampion.App.Views.Shell;

/// <summary>
/// Output → Send Art-Net: broadcasts <see cref="EditorState.CurrentFrame"/> at 40 Hz while switched on.
/// Problems go to the status bar; after a failed send it waits a second before trying again.
/// </summary>
internal sealed class ArtNetOutput : IDisposable
{
    private const long SendIntervalMs = 25;
    private const long RetryIntervalMs = 1000;

    private readonly StatusBar _status;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private ArtNetSender? _sender;
    private long _waitMs = SendIntervalMs;

    /// <summary>An output that reports to <paramref name="status"/>; off until <see cref="Toggle"/>d.</summary>
    public ArtNetOutput(StatusBar status) => _status = status;

    /// <summary>True while output is switched on.</summary>
    public bool IsSending => _sender is not null;

    /// <summary>Switches output off, or on; a socket that can't be opened leaves it off and says why.</summary>
    public void Toggle()
    {
        if (_sender is not null)
        {
            _sender.Dispose();
            _sender = null;
            return;
        }
        try
        {
            _sender = new ArtNetSender();
            _waitMs = SendIntervalMs;
            _status.Message = null;
        }
        catch (SocketException ex)
        {
            _status.Message = "Art-Net: " + ex.Message;
        }
    }

    /// <summary>Sends the current frame when one is due. Call every UI tick; allocation-free while sending works.</summary>
    public void Tick(EditorState state)
    {
        if (_sender is null || _clock.ElapsedMilliseconds < _waitMs)
            return;
        _clock.Restart();
        try
        {
            _sender.Send(state.CurrentFrame);
            _waitMs = SendIntervalMs;
        }
        catch (SocketException ex)
        {
            _status.Message = "Art-Net: " + ex.Message;
            _waitMs = RetryIntervalMs;
        }
    }

    /// <summary>Closes the socket.</summary>
    public void Dispose()
    {
        _sender?.Dispose();
        _sender = null;
    }
}
