using Avalonia;
using Avalonia.Input;
using Lampion.App.Editor;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Controls.Timeline;

/// <summary>
/// Routes pointer input on the timeline: works out what was pressed, starts the matching interaction and
/// feeds it pointer moves until release; also the wheel (scroll and zoom) and the hover cursor.
/// </summary>
internal sealed class TimelineInput
{
    private const double WheelZoom = 1.2;
    private const double WheelScrollPixels = 60;
    private const double WheelScrollRows = 40;
    private const double DoubleClickSnapPixels = 10;

    // Cursors need the windowing platform, so they are made with the control rather than statically.
    private readonly Cursor _arrowCursor = new(StandardCursorType.Arrow);
    private readonly Cursor _resizeCursor = new(StandardCursorType.SizeWestEast);
    private readonly Cursor _handCursor = new(StandardCursorType.Hand);

    private readonly EditorState _state;
    private readonly TimelineViewport _viewport;
    private readonly TimelineHitTester _hitTester;
    private readonly TimelineSnapper _snapper;
    private readonly TimelineContextMenus _menus;
    private readonly TrackHeaderActions _headers;
    private readonly KeyframeLaneActions _lanes;
    private readonly ScrubInteraction _scrub;
    private readonly TimelineControl _host;
    private ITimelineInteraction? _interaction;

    /// <summary>Input handling for <paramref name="host"/>, which forwards its pointer events here.</summary>
    public TimelineInput(EditorState state, TimelineViewport viewport, TimelineHitTester hitTester, TimelineSnapper snapper, TimelineControl host)
    {
        _state = state;
        _viewport = viewport;
        _hitTester = hitTester;
        _snapper = snapper;
        _host = host;
        _menus = new TimelineContextMenus(state, snapper, host);
        _headers = new TrackHeaderActions(state, _menus, host);
        _lanes = new KeyframeLaneActions(state, viewport, snapper, hitTester);
        _scrub = new ScrubInteraction(state, viewport);
    }

    /// <summary>Whether a drag is in progress (the timeline doesn't page during playback meanwhile).</summary>
    public bool IsInteracting => _interaction is not null;

    /// <summary>Handles a pointer press: scrub in the ruler, header clicks, keyframes, clip drags and menus, or select-and-seek.</summary>
    public void Pressed(PointerPressedEventArgs e)
    {
        var p = e.GetPosition(_host);
        var rightButton = e.GetCurrentPoint(_host).Properties.IsRightButtonPressed;
        var additive = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (p.Y < TimelineViewport.RulerHeight)
        {
            if (p.X > TimelineViewport.HeaderWidth)
            {
                Begin(_scrub, e);
                _scrub.Seek(p);
            }
            return;
        }

        if (!_hitTester.TryGetRow(p.Y, out var row, out var rowY))
        {
            _state.Deselect();
            return;
        }

        if (p.X < TimelineViewport.HeaderWidth)
        {
            _headers.Press(row, rowY, p, rightButton);
            return;
        }

        if (row.IsLane)
        {
            if (_lanes.Press(row, rowY, p, rightButton, e.ClickCount) is { } keyDrag)
                Begin(keyDrag, e);
            return;
        }

        var hasHit = _hitTester.TryHitClip(row, rowY, p, out var hit);
        if (rightButton)
        {
            if (hasHit)
            {
                if (!_state.SelectedClipIds.Contains(hit.Clip.Id))
                    _state.SelectClip(hit.Clip.Id);
                _menus.ClipMenu(hit.Clip, row.Track).Open(_host);
            }
            else
                _menus.TrackMenu(row.Track, _viewport.XToTime(p.X)).Open(_host);
            return;
        }

        if (hasHit)
        {
            if (!(_state.SelectedClipIds.Contains(hit.Clip.Id) && !additive))
                _state.SelectClip(hit.Clip.Id, additive);
            if (!row.Track.Locked)
                Begin(StartClipDrag(hit, row.Track, _viewport.XToTime(p.X)), e);
            return;
        }

        if (e.ClickCount == 2 && row.Track.Kind == TrackKind.Light)
        {
            TimelineOps.AddEffectClip(_state, row.Track, "solid", _snapper.Snap(_viewport.XToTime(p.X), _viewport.PixelsToSeconds(DoubleClickSnapPixels)));
            return;
        }
        _state.SelectTrack(row.Track.Id);
        _state.Seek(Math.Max(0, _viewport.XToTime(p.X)));
    }

    /// <summary>Feeds the drag in progress, or updates the hover cursor when there is none.</summary>
    public void Moved(PointerEventArgs e)
    {
        var p = e.GetPosition(_host);
        if (_interaction is { } interaction)
            interaction.Move(p, e.KeyModifiers);
        else
            UpdateCursor(p);
    }

    /// <summary>Ends the drag in progress (committing its edit) and releases the pointer.</summary>
    public void Released(PointerReleasedEventArgs e)
    {
        e.Pointer.Capture(null);
        var interaction = _interaction;
        _interaction = null;
        interaction?.Complete();
    }

    /// <summary>Ctrl/Alt+wheel zooms around the pointer, Shift+wheel or a sideways wheel scrolls in time, the plain wheel scrolls the rows.</summary>
    public void Wheel(PointerWheelEventArgs e, double contentHeight)
    {
        var p = e.GetPosition(_host);
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Alt))
            _host.ZoomAround(e.Delta.Y > 0 ? WheelZoom : 1 / WheelZoom, Math.Max(TimelineViewport.HeaderWidth, p.X));
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) || Math.Abs(e.Delta.X) > Math.Abs(e.Delta.Y))
        {
            var delta = Math.Abs(e.Delta.X) > 0 ? e.Delta.X : e.Delta.Y;
            _host.ScrollX = Math.Max(0, _host.ScrollX - delta * WheelScrollPixels / _viewport.PixelsPerSecond);
        }
        else
            _host.ScrollY = Math.Clamp(_host.ScrollY - e.Delta.Y * WheelScrollRows, 0, Math.Max(0, contentHeight - _viewport.Height + TimelineViewport.RulerHeight));
        e.Handled = true;
    }

    private ITimelineInteraction StartClipDrag(ClipHit hit, Track track, double time) =>
        hit.Zone switch
        {
            ClipZone.TrimStart => new ClipTrimInteraction(_state, _viewport, _snapper, hit.Clip, track, time, trimStart: true),
            ClipZone.TrimEnd => new ClipTrimInteraction(_state, _viewport, _snapper, hit.Clip, track, time, trimStart: false),
            ClipZone.FadeIn => new ClipFadeInteraction(_state, _viewport, _snapper, (LightClip)hit.Clip, track, time, fadeIn: true),
            ClipZone.FadeOut => new ClipFadeInteraction(_state, _viewport, _snapper, (LightClip)hit.Clip, track, time, fadeIn: false),
            ClipZone.Transition => new TransitionResizeInteraction(_state, _viewport, _snapper, (LightClip)hit.Clip, track, time),
            _ => new ClipMoveInteraction(_state, _viewport, _snapper, _hitTester, hit.Clip, track, time),
        };

    private void Begin(ITimelineInteraction interaction, PointerPressedEventArgs e)
    {
        _interaction = interaction;
        e.Pointer.Capture(_host);
    }

    private void UpdateCursor(Point p)
    {
        var cursor = _arrowCursor;
        if (p.X > TimelineViewport.HeaderWidth
            && _hitTester.TryGetRow(p.Y, out var row, out var rowY)
            && !row.IsLane
            && _hitTester.TryHitClip(row, rowY, p, out var hit))
            cursor = hit.Zone switch
            {
                ClipZone.TrimStart or ClipZone.TrimEnd or ClipZone.Transition => _resizeCursor,
                ClipZone.FadeIn or ClipZone.FadeOut => _handCursor,
                _ => _arrowCursor,
            };
        if (!ReferenceEquals(_host.Cursor, cursor))
            _host.Cursor = cursor;
    }
}
