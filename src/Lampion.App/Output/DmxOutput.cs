using System.Diagnostics;
using System.Net.Sockets;
using Lampion.App.Editor;
using Lampion.App.Views.Shell;
using Lampion.Core.Engine;
using Lampion.Core.Output;

namespace Lampion.App.Output;

/// <summary>
/// Output → Send DMX: sends <see cref="EditorState.CurrentFrame"/> at about 40 Hz to the device in <see cref="Settings"/>.
/// Sending runs on its own thread, since a serial interface can block for a whole frame; a device that fails or
/// is unplugged is reopened every second, and the reason goes to the status bar. Stopping sends one dark frame.
/// Public members are UI thread only.
/// </summary>
internal sealed class DmxOutput : IDisposable
{
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(25);
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan SleepSlice = TimeSpan.FromMilliseconds(50);

    private readonly StatusBar _status;
    private readonly object _gate = new();
    private readonly DmxFrame _latest = new();
    private readonly DmxFrame _sending = new();
    private Thread? _worker;
    private volatile bool _stop;
    private volatile string? _problem;
    private string? _reported;
    private string _label;
    private volatile IDmxFrameSource? _source;

    /// <summary>An output that reports to <paramref name="status"/>, set up from the saved user settings; off until started.</summary>
    public DmxOutput(StatusBar status)
    {
        _status = status;
        Settings = UserData.LoadSettings().Output;
        _label = Settings.Describe();
    }

    /// <summary>Where output goes; change it with <see cref="Configure"/>.</summary>
    public OutputSettings Settings { get; private set; }

    /// <summary>True while output is switched on, even if the device is currently failing (see <see cref="Problem"/>).</summary>
    public bool IsSending => _worker is not null;

    /// <summary>What is being sent to, e.g. "Art-Net → broadcast", while sending; null when off. Cached, so free to read every tick.</summary>
    public string? Label => _worker is null ? null : _label;

    /// <summary>Why the last open or send failed, in words; null while frames are going out or output is off.</summary>
    public string? Problem => _worker is null ? null : _problem;

    /// <summary>Switches output off, or on with the current <see cref="Settings"/>.</summary>
    public void Toggle()
    {
        if (IsSending)
            Stop();
        else
            Start();
    }

    /// <summary>Starts sending; settings that can't work leave it off and say why in the status bar.</summary>
    public void Start()
    {
        if (_worker is not null)
            return;
        if (Settings.Problem() is { } problem)
        {
            _status.Message = "Output: " + problem;
            return;
        }
        _stop = false;
        _problem = null;
        _reported = null;
        var settings = Settings;
        _worker = new Thread(() => Run(settings)) { IsBackground = true, Name = "DMX output" };
        _worker.Start();
    }

    /// <summary>Sends a dark frame, closes the device and stops.</summary>
    public void Stop()
    {
        if (_worker is null)
            return;
        _stop = true;
        _worker.Join(StopTimeout);
        _worker = null;
    }

    /// <summary>Saves <paramref name="settings"/> for this machine and restarts output on them when <paramref name="send"/>.</summary>
    public void Configure(OutputSettings settings, bool send)
    {
        Stop();
        Settings = settings;
        _label = settings.Describe();
        UserData.SaveSettings(UserData.LoadSettings() with { Output = settings });
        if (send)
            Start();
    }

    /// <summary>
    /// Where the sender thread takes frames from directly, bypassing <see cref="Tick"/>: live mode puts its engine
    /// here so frames go engine thread → sender thread → device without waiting on the UI. Null sends the editor's frame.
    /// </summary>
    public IDmxFrameSource? Source
    {
        get => _source;
        set => _source = value;
    }

    /// <summary>Hands the current frame to the sender and reports new problems. Call every UI tick; allocation-free while sending works.</summary>
    public void Tick(EditorState state)
    {
        if (_worker is null)
            return;
        if (_source is null)
            lock (_gate)
                state.CurrentFrame.CopyTo(_latest);
        var problem = _problem;
        if (ReferenceEquals(problem, _reported))
            return;
        _reported = problem;
        if (problem is not null)
            _status.Message = "Output: " + problem;
    }

    /// <summary>Stops output.</summary>
    public void Dispose() => Stop();

    private void Run(OutputSettings settings)
    {
        IDmxOutput? device = null;
        try
        {
            // Frames are due on a fixed schedule rather than "interval after the last one", so the OS rounding
            // each sleep up to its timer tick averages out instead of slowing every frame.
            var due = Stopwatch.GetTimestamp();
            var interval = (long)(FrameInterval.TotalSeconds * Stopwatch.Frequency);
            while (!_stop)
            {
                device ??= TryOpen(settings);
                if (device is null)
                {
                    Wait(RetryInterval);
                    continue;
                }
                if (_source is { } source)
                    source.CopyLatestTo(_sending);
                else
                    lock (_gate)
                        _latest.CopyTo(_sending);
                if (!TrySend(device, _sending))
                {
                    device.Dispose();
                    device = null;
                    Wait(RetryInterval);
                    continue;
                }
                _problem = null;
                var now = Stopwatch.GetTimestamp();
                due = Math.Max(due + interval, now - interval);
                Wait(Stopwatch.GetElapsedTime(now, due));
            }
            if (device is not null)
                TrySend(device, Dark(_sending));
        }
        finally
        {
            device?.Dispose();
        }
    }

    private bool TrySend(IDmxOutput device, DmxFrame frame)
    {
        try
        {
            device.Send(frame);
            return true;
        }
        catch (Exception ex) when (ex is IOException or SocketException or InvalidOperationException or UnauthorizedAccessException)
        {
            _problem = ex.Message;
            return false;
        }
    }

    private IDmxOutput? TryOpen(OutputSettings settings)
    {
        var port = settings.SerialPort ?? "";
        try
        {
            return settings.Kind switch
            {
                DmxOutputKind.EnttecPro => new EnttecProSender(port, settings.Universe),
                DmxOutputKind.OpenDmx => new OpenDmxSender(port, settings.Universe),
                _ => new ArtNetSender(settings.ArtNetHost),
            };
        }
        catch (UnauthorizedAccessException)
        {
            _problem = $"{port} is in use by another program; close QLC+ or the interface's own tool.";
        }
        catch (Exception ex) when (ex is IOException or ArgumentException && settings.Kind != DmxOutputKind.ArtNet)
        {
            _problem = $"Can't open {port}; is the interface plugged in? ({ex.Message})";
        }
        catch (Exception ex) when (ex is SocketException or FormatException)
        {
            _problem = ex.Message;
        }
        return null;
    }

    // Every universe that was being sent, with all channels at zero, so the rig goes dark rather than freezing.
    private static DmxFrame Dark(DmxFrame frame)
    {
        var dark = new DmxFrame();
        foreach (var universe in frame)
            dark.Universe(universe.Number);
        return dark;
    }

    private void Wait(TimeSpan time)
    {
        var until = Stopwatch.GetTimestamp() + (long)(Math.Max(0, time.TotalSeconds) * Stopwatch.Frequency);
        while (!_stop)
        {
            var left = Stopwatch.GetElapsedTime(Stopwatch.GetTimestamp(), until);
            if (left <= TimeSpan.Zero)
                return;
            Thread.Sleep(left < SleepSlice ? left : SleepSlice);
        }
    }
}
