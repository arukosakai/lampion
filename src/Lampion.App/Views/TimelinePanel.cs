using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Lampion.App.Controls;
using Lampion.App.Editor;

namespace Lampion.App.Views;

/// <summary>Transport bar, the timeline itself and its scrollbars.</summary>
public sealed class TimelinePanel : DockPanel
{
    private readonly EditorState _state;
    private readonly ScrollBar _horizontal = new() { Orientation = Orientation.Horizontal, AllowAutoHide = false };
    private readonly ScrollBar _vertical = new() { Orientation = Orientation.Vertical, AllowAutoHide = false };
    private readonly Button _play;
    private bool _syncing;

    public TimelinePanel(EditorState state)
    {
        _state = state;
        Timeline = new TimelineControl(state);

        _play = Ui.Button("▶", state.TogglePlay, "Play / pause (Space)");
        _play.Width = 40;
        var loop = Ui.Toggle("⟲", state.Loop, on => state.Loop = on, "Loop playback");
        var bpm = Ui.Number(state.Project.Bpm, v =>
        {
            if (Math.Abs(state.Project.Bpm - v) < 1e-9)
                return;
            state.Project.Bpm = v;
            state.Commit();
        }, 20, 300, 1, "0.#");
        bpm.Width = 120;
        ToolTip.SetTip(bpm, "Tempo — the beat grid and snapping follow it");
        var offset = Ui.Number(state.Project.BeatOffset, v =>
        {
            if (Math.Abs(state.Project.BeatOffset - v) < 1e-9)
                return;
            state.Project.BeatOffset = v;
            state.Commit();
        }, 0, 60, 0.01, "0.00");
        offset.Width = 120;
        ToolTip.SetTip(offset, "Seconds to the first downbeat, to line the grid up with the music");
        var length = Ui.Number(state.Project.Duration, v =>
        {
            if (Math.Abs(state.Project.Duration - v) < 1e-9)
                return;
            state.Project.Duration = v;
            state.Commit();
        }, 1, 36000, 1, "0.#");
        length.Width = 120;
        ToolTip.SetTip(length, "Show length in seconds");
        state.ProjectReplaced += () =>
        {
            bpm.Value = (decimal)state.Project.Bpm;
            offset.Value = (decimal)state.Project.BeatOffset;
            length.Value = (decimal)state.Project.Duration;
        };

        var bar = new WrapPanel { Margin = new Thickness(6, 4) };
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
                     Ui.Button("−", () => Timeline.ZoomAround(1 / 1.4, TimelineControl.HeaderWidth + Timeline.VisibleSeconds * Timeline.PixelsPerSecond / 2), "Zoom out (-)"),
                     Ui.Button("Fit", Timeline.ZoomToFit, "Zoom to fit (\\)"),
                     Ui.Button("+", () => Timeline.ZoomAround(1.4, TimelineControl.HeaderWidth + Timeline.VisibleSeconds * Timeline.PixelsPerSecond / 2), "Zoom in (=)"),
                 })
        {
            c.Margin = new Thickness(0, 0, 4, 0);
            c.VerticalAlignment = VerticalAlignment.Center;
            bar.Children.Add(c);
        }

        SetDock(bar, Dock.Top);
        Children.Add(bar);
        SetDock(_horizontal, Dock.Bottom);
        Children.Add(_horizontal);
        SetDock(_vertical, Dock.Right);
        Children.Add(_vertical);
        Children.Add(Timeline);

        _horizontal.Scroll += (_, _) => ScrollFromBars();
        _vertical.Scroll += (_, _) => ScrollFromBars();
        _horizontal.PropertyChanged += (_, e) =>
        {
            if (e.Property == RangeBase.ValueProperty)
                ScrollFromBars();
        };
        _vertical.PropertyChanged += (_, e) =>
        {
            if (e.Property == RangeBase.ValueProperty)
                ScrollFromBars();
        };
        Timeline.ViewChanged += SyncBars;
        Timeline.SizeChanged += (_, _) => SyncBars();
        state.Changed += SyncBars;
        state.PlayheadChanged += () => _play.Content = state.IsPlaying ? "⏸" : "▶";
    }

    public TimelineControl Timeline { get; }

    private static Control Gap() => new Border { Width = 10 };

    private void ScrollFromBars()
    {
        if (_syncing)
            return;
        Timeline.ScrollX = _horizontal.Value;
        Timeline.ScrollY = _vertical.Value;
        Timeline.InvalidateVisual();
    }

    private void SyncBars()
    {
        _syncing = true;
        var visible = Timeline.VisibleSeconds;
        _horizontal.Maximum = Math.Max(0, Timeline.ContentSeconds - visible);
        _horizontal.ViewportSize = visible;
        _horizontal.Value = Math.Min(Timeline.ScrollX, _horizontal.Maximum);
        var visibleHeight = Math.Max(0, Timeline.Bounds.Height - TimelineControl.RulerHeight);
        _vertical.Maximum = Math.Max(0, Timeline.ContentHeight - visibleHeight);
        _vertical.ViewportSize = visibleHeight;
        _vertical.Value = Math.Min(Timeline.ScrollY, _vertical.Maximum);
        _syncing = false;
    }
}
