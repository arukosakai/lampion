using Avalonia.Threading;
using Lampion.App.Controls;
using Lampion.App.Editor;
using Lampion.App.Media;
using Lampion.App.Output;

namespace Lampion.App.Views.Shell;

/// <summary>
/// The one clock that drives the workspace, about 66 times a second on the UI thread: advance the
/// playhead, slave the media, rebuild the 3D scene, show video, send DMX, then refresh the status text.
/// Every step is allocation-free in steady state.
/// </summary>
internal sealed class FrameLoop
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(15);

    private readonly DispatcherTimer _timer = new(DispatcherPriority.Render) { Interval = Interval };
    private readonly EditorState _state;
    private readonly MediaEngine _media;
    private readonly StageView _stage;
    private readonly VideoMonitor _video;
    private readonly DmxOutput _output;
    private readonly StatusBar _status;
    private readonly WindowTitle _title;

    /// <summary>A stopped loop over the workspace's per-frame parts.</summary>
    public FrameLoop(EditorState state, MediaEngine media, StageView stage, VideoMonitor video, DmxOutput output, StatusBar status, WindowTitle title)
    {
        _state = state;
        _media = media;
        _stage = stage;
        _video = video;
        _output = output;
        _status = status;
        _title = title;
        _timer.Tick += OnTick;
    }

    /// <summary>Starts ticking.</summary>
    public void Start() => _timer.Start();

    /// <summary>Stops ticking, e.g. before the parts it drives are disposed.</summary>
    public void Stop() => _timer.Stop();

    /// <summary>Runs one frame now.</summary>
    public void Tick()
    {
        _state.Tick();
        _media.Sync(_state);
        _stage.UpdateScene();
        _video.Update();
        _output.Tick(_state);
        _status.Refresh(_state, _output.Label);
        _title.Refresh();
    }

    private void OnTick(object? sender, EventArgs e) => Tick();
}
