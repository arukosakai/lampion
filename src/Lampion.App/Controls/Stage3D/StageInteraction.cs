using System.Numerics;
using Avalonia;
using Avalonia.Input;
using Lampion.App.Editor;

namespace Lampion.App.Controls.Stage3D;

/// <summary>
/// Decides what a press on the stage view starts — orbit, aim, gizmo axis, move or click — and routes the
/// following moves and the release to that drag. One instance of each drag is reused, so pressing allocates nothing.
/// </summary>
internal sealed class StageInteraction
{
    // How near (pixels) a press must land to a gizmo arrow to grab it, and how short an arrow may get before it can't be.
    private const float ArrowReach = 9;
    private const float ShortestArrow = 4;

    private readonly StageView _view;
    private readonly EditorState _state;
    private readonly StagePicker _picker;
    private readonly SelectionEditor _editor;
    private readonly OrbitDrag _orbit;
    private readonly ClickDrag _click;
    private readonly AimDrag _aim;
    private readonly AxisDrag _axis;
    private readonly PlaneDrag _plane;
    private IStageDrag? _active;
    private Point _lastPointer;
    private int? _hoveredAxis;

    /// <summary>Handles input for <paramref name="view"/>, editing through <paramref name="editor"/>.</summary>
    public StageInteraction(StageView view, EditorState state, StagePicker picker, SelectionEditor editor)
    {
        (_view, _state, _picker, _editor) = (view, state, picker, editor);
        _orbit = new OrbitDrag(view);
        _click = new ClickDrag(view, state);
        _aim = new AimDrag(view, state, editor);
        _axis = new AxisDrag(state, editor);
        _plane = new PlaneDrag(view, state, editor);
    }

    /// <summary>Axis of the gizmo arrow to highlight (0 X, 1 Y, 2 Z): the one being dragged, else the one under the pointer; null for none.</summary>
    public int? HighlightedAxis => ReferenceEquals(_active, _axis) ? _axis.Axis : _active is null ? _hoveredAxis : null;

    /// <summary>A button went down at <paramref name="point"/>.</summary>
    public void Press(Point point, PointerPointProperties buttons, KeyModifiers modifiers)
    {
        _lastPointer = point;
        _active = null;

        // The Hand tool never edits: every button just moves the camera.
        if (buttons.IsRightButtonPressed || buttons.IsMiddleButtonPressed || (_view.Tool == BuilderTool.Hand && buttons.IsLeftButtonPressed))
        {
            _orbit.Begin(pan: buttons.IsMiddleButtonPressed || modifiers.HasFlag(KeyModifiers.Shift));
            _active = _orbit;
            return;
        }
        if (!buttons.IsLeftButtonPressed)
            return;

        if (_view.BuildMode && _view.Tool == BuilderTool.Aim && _state.SelectedFixtureIds.Count > 0)
        {
            _aim.Begin(point);
            _active = _aim;
            return;
        }

        _editor.PieceOnly = false;
        if (ArrowsGrabbable && TryGrabArrow(point))
            return;

        var additive = modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta);
        var (rayOrigin, direction) = _view.RayAt(point);
        if (_picker.Pick(rayOrigin, direction) is { } picked)
        {
            Select(picked, additive);
            // Set pieces are selected one at a time, so Ctrl on one means "just this piece, not its structure".
            _editor.PieceOnly = additive && !picked.IsFixture;
            if (_view.BuildMode && (!additive || _editor.PieceOnly) && _view.GizmoOrigin is { } origin)
            {
                var vertical = modifiers.HasFlag(KeyModifiers.Alt);
                var grab = vertical ? origin : StageGeometry.IntersectPlaneY(rayOrigin, direction, origin.Y) ?? origin;
                _plane.Begin(origin.Y, grab, vertical);
                _active = _plane;
            }
            return;
        }
        _click.Begin(point);
        _active = _click;
    }

    private bool TryGrabArrow(Point point)
    {
        if (_view.Gizmo() is not { } gizmo || ArrowAt(point, gizmo) is not { } axis)
            return false;
        _axis.Begin(point, axis, gizmo.Tip(axis) - gizmo.Origin, gizmo.Length);
        _active = _axis;
        return true;
    }

    // Shared by press and hover, so the arrow that lights up is the one a click would grab.
    private static int? ArrowAt(Point point, in GizmoHandles gizmo)
    {
        var p = new Vector2((float)point.X, (float)point.Y);
        for (var axis = 0; axis < 3; axis++)
        {
            var tip = gizmo.Tip(axis);
            if (StageGeometry.DistanceToSegment(p, gizmo.Origin, tip) > ArrowReach)
                continue;
            if ((tip - gizmo.Origin).Length() < ShortestArrow)
                continue;
            return axis;
        }
        return null;
    }

    private bool ArrowsGrabbable => _view.BuildMode && !(_view.Tool == BuilderTool.Aim && _state.SelectedFixtureIds.Count > 0);

    private void Select(StagePick picked, bool additive)
    {
        if (picked.IsFixture)
        {
            if (!_state.SelectedFixtureIds.Contains(picked.Id) || additive)
                _state.SelectFixtures([picked.Id], additive);
        }
        else if (_state.SelectedObjectId != picked.Id)
            _state.SelectObject(picked.Id);
    }

    /// <summary>The pointer moved to <paramref name="point"/>, button held or not.</summary>
    public void Move(Point point)
    {
        var delta = point - _lastPointer;
        _lastPointer = point;
        _active?.Move(point, delta);
        _hoveredAxis = _active is null && ArrowsGrabbable && _view.Gizmo() is { } gizmo ? ArrowAt(point, gizmo) : null;
    }

    /// <summary>The button came up at <paramref name="point"/>; ends the drag.</summary>
    public void Release(Point point)
    {
        var drag = _active;
        _active = null;
        drag?.Release(point);
    }

    /// <summary>The pointer left the view; drops the hover highlight.</summary>
    public void Leave() => _hoveredAxis = null;
}
