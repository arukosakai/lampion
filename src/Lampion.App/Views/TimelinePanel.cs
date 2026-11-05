using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Lampion.App.Controls;
using Lampion.App.Editor;

namespace Lampion.App.Views;

/// <summary>The timeline panel: transport bar on top, the timeline itself, and scrollbars kept in step with its view.</summary>
internal sealed class TimelinePanel : DockPanel
{
    private readonly ScrollBar _horizontal = new() { Orientation = Orientation.Horizontal, AllowAutoHide = false };
    private readonly ScrollBar _vertical = new() { Orientation = Orientation.Vertical, AllowAutoHide = false };
    private bool _syncing;

    /// <summary>A panel showing <paramref name="state"/>'s timeline.</summary>
    public TimelinePanel(EditorState state)
    {
        Timeline = new TimelineControl(state);
        var bar = new TimelineTransportBar(state, Timeline);

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
    }

    /// <summary>The timeline control, for panels that drag onto it and for zoom shortcuts.</summary>
    public TimelineControl Timeline { get; }

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
