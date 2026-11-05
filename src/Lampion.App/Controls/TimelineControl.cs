using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Lampion.App.Controls.Timeline;
using Lampion.App.Editor;
using Lampion.Core.Model.Timeline;
using Lampion.Core.Persistence;

namespace Lampion.App.Controls;

/// <summary>
/// The After Effects-style timeline: track headers on the left, clips on the right, keyframe rows under
/// expanded light tracks, and a ruler with the beat grid. This control only wires its collaborators
/// together (layout, renderers, hit testing, interactions, drops) and forwards input to them. The playhead
/// sits on its own child layer, so playback repaints only that. UI thread only.
/// </summary>
public sealed class TimelineControl : Control
{
    /// <summary>Width of the track-header column, in pixels.</summary>
    public const double HeaderWidth = TimelineViewport.HeaderWidth;

    /// <summary>Height of the ruler, in pixels.</summary>
    public const double RulerHeight = TimelineViewport.RulerHeight;

    // Scrolling past the content leaves room to drop clips after the end and below the last track.
    private const double TrailingSeconds = 30;
    private const double TrailingPixels = 40;
    private const double PageMargin = 20;
    private const double PageBack = 0.1;
    private const double DefaultDropBeats = 8;
    private const double ZoomButtonFactor = 1.4;

    private readonly EditorState _state;
    private readonly TimelineViewport _viewport;
    private readonly TimelineLayout _layout;
    private readonly TimelineRenderer _renderer;
    private readonly PlayheadLayer _playheadLayer;
    private readonly TimelineInput _input;
    private readonly TimelineDropTarget _drops;

    /// <summary>A timeline showing <paramref name="state"/>'s project; it repaints itself on edits, selection and playhead changes.</summary>
    public TimelineControl(EditorState state)
    {
        _state = state;
        Focusable = true;
        ClipToBounds = true;
        _viewport = new TimelineViewport(this);
        _layout = new TimelineLayout(state);
        var shapes = new TimelineShapes();
        var snapper = new TimelineSnapper(state, _viewport);
        var hitTester = new TimelineHitTester(_viewport, _layout);
        _renderer = new TimelineRenderer(state, _viewport, _layout, shapes);
        _playheadLayer = new PlayheadLayer(new PlayheadRenderer(state, _viewport, shapes));
        _input = new TimelineInput(state, _viewport, hitTester, snapper, this);
        _drops = new TimelineDropTarget(state, _viewport, _layout, hitTester, snapper);
        VisualChildren.Add(_playheadLayer);
        state.Changed += InvalidateVisual;
        state.SelectionChanged += InvalidateVisual;
        state.PlayheadChanged += OnPlayhead;
    }

    /// <summary>Raised when zoom, scroll or the row layout changes, so scrollbars can follow.</summary>
    public event Action? ViewChanged;

    /// <summary>Horizontal zoom in pixels per second (2..1200 when set through the zoom methods).</summary>
    public double PixelsPerSecond
    {
        get => _viewport.PixelsPerSecond;
        set
        {
            _viewport.PixelsPerSecond = value;
            _playheadLayer.InvalidateVisual();
        }
    }

    /// <summary>Show time at the left edge of the clip area, in seconds.</summary>
    public double ScrollX
    {
        get => _viewport.ScrollX;
        set
        {
            _viewport.ScrollX = value;
            _playheadLayer.InvalidateVisual();
        }
    }

    /// <summary>Pixels the rows are scrolled up under the ruler.</summary>
    public double ScrollY
    {
        get => _viewport.ScrollY;
        set => _viewport.ScrollY = value;
    }

    /// <summary>Seconds of show time the clip area shows at the current zoom.</summary>
    public double VisibleSeconds => _viewport.VisibleSeconds;

    /// <summary>Seconds the horizontal scroll range covers: the content plus room after it.</summary>
    public double ContentSeconds => _state.Project.ContentEnd + TrailingSeconds;

    /// <summary>Pixels the vertical scroll range covers: all rows plus room below them, or 0 with no tracks.</summary>
    public double ContentHeight => _layout.Rows.Length == 0 ? 0 : _layout.ContentBottom + TrailingPixels;

    /// <summary>The <c>HH:MM:SS:FF</c> timecode of <paramref name="seconds"/>. Allocates a string.</summary>
    /// <param name="seconds">Show time, 0 or more.</param>
    /// <param name="fps">Frames per second for the last field.</param>
    public static string Timecode(double seconds, int fps = 30) => TimecodeText.Format(seconds, fps);

    /// <summary>Zooms by <paramref name="factor"/>, keeping the time under <paramref name="anchorX"/> (control x) in place.</summary>
    public void ZoomAround(double factor, double anchorX)
    {
        _viewport.ZoomAround(factor, anchorX);
        _playheadLayer.InvalidateVisual();
        NotifyViewChanged();
        InvalidateVisual();
    }

    /// <summary>Zooms in (<paramref name="zoomIn"/>) or out one step around the middle of the clip area, like the transport buttons.</summary>
    public void ZoomStep(bool zoomIn) =>
        ZoomAround(zoomIn ? ZoomButtonFactor : 1 / ZoomButtonFactor, HeaderWidth + VisibleSeconds * PixelsPerSecond / 2);

    /// <summary>Zooms so the whole show fits the clip area and scrolls to the start.</summary>
    public void ZoomToFit()
    {
        _viewport.ZoomToFit(_state.Project.ContentEnd);
        _playheadLayer.InvalidateVisual();
        NotifyViewChanged();
        InvalidateVisual();
    }

    /// <summary>Shows where a drag from another panel would land; pass null to clear.</summary>
    /// <param name="p">Pointer position in this control's coordinates, or null.</param>
    /// <param name="kind">What is being dragged.</param>
    /// <param name="length">Seconds the dropped clip will last; defaults to eight beats.</param>
    public void PreviewDrop(Point? p, DropKind kind = DropKind.Effect, double? length = null)
    {
        _drops.UpdatePreview(p, kind, length ?? _state.SecondsPerBeat * DefaultDropBeats);
        InvalidateVisual();
    }

    /// <summary>Drops a new <paramref name="effectKind"/> clip on the light track at <paramref name="p"/>, snapped.</summary>
    /// <returns>False when <paramref name="p"/> isn't over a light track's clip area.</returns>
    public bool Drop(string effectKind, Point p)
    {
        PreviewDrop(null);
        return _drops.DropEffect(effectKind, p);
    }

    /// <summary>Drops a clip made from <paramref name="preset"/> on the light track at <paramref name="p"/>, snapped.</summary>
    /// <returns>False when <paramref name="p"/> isn't over a light track's clip area.</returns>
    public bool DropPreset(EffectPreset preset, Point p)
    {
        PreviewDrop(null);
        return _drops.DropPreset(preset, p);
    }

    /// <summary>
    /// Fixtures or groups dropped from the Project panel: onto a light track they join its targets,
    /// anywhere else they get a new light track named <paramref name="trackName"/>.
    /// </summary>
    /// <returns>False when <paramref name="p"/> is outside the track area or nothing was dragged.</returns>
    public bool DropTargets(IReadOnlyCollection<string> fixtureIds, IReadOnlyCollection<string> groupIds, string trackName, Point p)
    {
        PreviewDrop(null);
        return _drops.DropTargets(fixtureIds, groupIds, trackName, p);
    }

    /// <summary>Gives the light clip at <paramref name="p"/> a transition in of <paramref name="kind"/>.</summary>
    /// <returns>False when there's no light clip at <paramref name="p"/>.</returns>
    public bool DropTransition(TransitionKind kind, Point p)
    {
        PreviewDrop(null);
        return _drops.DropTransition(kind, p);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context) => _renderer.Render(context, _drops.Preview);

    /// <summary>Raises <see cref="ViewChanged"/>; for collaborators that change the layout or the view.</summary>
    internal void NotifyViewChanged() => ViewChanged?.Invoke();

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        _playheadLayer.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        _input.Pressed(e);
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _input.Moved(e);
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _input.Released(e);
    }

    /// <inheritdoc />
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        _input.Wheel(e, ContentHeight);
        NotifyViewChanged();
        InvalidateVisual();
    }

    private void OnPlayhead()
    {
        // Page forward like After Effects when playback runs off the right edge.
        if (_state.IsPlaying && !_input.IsInteracting)
        {
            var x = _viewport.TimeToX(_state.Playhead);
            if (x > Bounds.Width - PageMargin || x < HeaderWidth)
            {
                ScrollX = Math.Max(0, _state.Playhead - VisibleSeconds * PageBack);
                NotifyViewChanged();
                InvalidateVisual();
            }
        }
        // Only the playhead moved: repaint its layer, not the whole timeline under it.
        _playheadLayer.InvalidateVisual();
    }
}
