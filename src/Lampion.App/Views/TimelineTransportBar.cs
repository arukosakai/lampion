using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Lampion.App.Controls;
using Lampion.App.Editor;

namespace Lampion.App.Views;

/// <summary>
/// The bar above the timeline: transport (start, play/pause, stop, loop), tempo, beat offset and show
/// length, track/razor/marker actions and zoom.
/// </summary>
internal sealed class TimelineTransportBar : WrapPanel
{
    private const string PlayGlyph = "▶";
    private const string PauseGlyph = "⏸";

    private readonly EditorState _state;
    private readonly Button _play;
    private bool _showsPause;

    /// <summary>A bar driving <paramref name="state"/>'s transport and zooming <paramref name="timeline"/>.</summary>
    public TimelineTransportBar(EditorState state, TimelineControl timeline)
    {
        _state = state;
        Margin = new Thickness(6, 4);
        _play = Ui.Button(PlayGlyph, state.TogglePlay, "Play / pause (Space)");
        _play.Width = 40;
        var loop = Ui.Toggle("⟲", state.Loop, on => state.Loop = on, "Loop playback");
        var bpm = ProjectNumber(() => state.Project.Bpm, v => state.Project.Bpm = v, 20, 300, 1, "0.#", "Tempo — the beat grid and snapping follow it");
        var offset = ProjectNumber(() => state.Project.BeatOffset, v => state.Project.BeatOffset = v, 0, 60, 0.01, "0.00", "Seconds to the first downbeat, to line the grid up with the music");
        var length = ProjectNumber(() => state.Project.Duration, v => state.Project.Duration = v, 1, 36000, 1, "0.#", "Show length in seconds");
        state.ProjectReplaced += () =>
        {
            bpm.Value = (decimal)state.Project.Bpm;
            offset.Value = (decimal)state.Project.BeatOffset;
            length.Value = (decimal)state.Project.Duration;
        };

        foreach (var c in new Control[]
                 {
                     Ui.Button("⏮", () => state.Seek(0), "Go to start (Home)"),
                     _play,
                     Ui.Button("⏹", () =>
                     {
                         state.Pause();
                         state.Seek(0);
                     }, "Stop"),
                     loop,
                     Gap(),
                     Ui.Label("BPM"), bpm, Ui.Label("Offset"), offset, Ui.Label("Length"), length,
                     Gap(),
                     Ui.Button("+ Light track", () => TimelineOps.AddLightTrack(state, state.SelectedFixtureIds), "New light track (with the selected fixtures)"),
                     Ui.Button("✂ Split", () => TimelineOps.SplitAtPlayhead(state), "Split at playhead (Ctrl+K)"),
                     Ui.Button("◆ Marker", () => TimelineOps.AddMarker(state), "Add marker at playhead (M)"),
                     Gap(),
                     Ui.Button("−", () => timeline.ZoomStep(zoomIn: false), "Zoom out (-)"),
                     Ui.Button("Fit", timeline.ZoomToFit, "Zoom to fit (\\)"),
                     Ui.Button("+", () => timeline.ZoomStep(zoomIn: true), "Zoom in (=)"),
                 })
        {
            c.Margin = new Thickness(0, 0, 4, 0);
            c.VerticalAlignment = VerticalAlignment.Center;
            Children.Add(c);
        }

        state.PlayheadChanged += UpdatePlayButton;
    }

    private static Control Gap() => new Border { Width = 10 };

    /// <summary>A number box bound to a project setting; each change commits an undo step.</summary>
    private NumericUpDown ProjectNumber(Func<double> get, Action<double> set, double min, double max, double step, string format, string tip)
    {
        var box = Ui.Number(get(), v =>
        {
            if (Math.Abs(get() - v) < 1e-9)
                return;
            set(v);
            _state.Commit();
        }, min, max, step, format);
        box.Width = 120;
        ToolTip.SetTip(box, tip);
        return box;
    }

    // Runs every frame during playback, so it only touches the button when the state flips.
    private void UpdatePlayButton()
    {
        var playing = _state.IsPlaying;
        if (playing == _showsPause)
            return;
        _showsPause = playing;
        _play.Content = playing ? PauseGlyph : PlayGlyph;
    }
}
