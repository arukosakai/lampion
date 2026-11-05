using System.Numerics;
using Avalonia;
using Lampion.App.Editor;
using Lampion.Core.Model.Stage;

namespace Lampion.App.Controls.Stage3D;

/// <summary>
/// Dragging a picked fixture or set piece: it slides across the horizontal plane it sits in, or, with Alt,
/// up and down a vertical plane facing the camera. Snapped to the grid; one undo step on release.
/// </summary>
internal sealed class PlaneDrag(StageView view, EditorState state, SelectionEditor editor) : IStageDrag
{
    private float _planeY;
    private Vector3 _grab;
    private bool _vertical;
    private bool _moved;

    /// <summary>Starts a drag of the selection.</summary>
    /// <param name="planeY">Height of the horizontal plane, metres.</param>
    /// <param name="grab">The world point under the pointer at the press, which the pointer then carries.</param>
    /// <param name="vertical">True to change only the height.</param>
    public void Begin(float planeY, Vector3 grab, bool vertical)
    {
        (_planeY, _grab, _vertical, _moved) = (planeY, grab, vertical, false);
        editor.CaptureOrigins();
    }

    /// <inheritdoc/>
    public void Move(Point point, Avalonia.Vector delta)
    {
        var (origin, direction) = view.RayAt(point);
        var step = state.Project.Stage.GridStep;
        Vec3 d;
        if (_vertical)
        {
            // Height drag: intersect a vertical plane facing the camera through the grab point.
            var (_, _, forward) = view.Camera.Basis();
            var normal = Vector3.Normalize(new Vector3(forward.X, 0, forward.Z));
            var denom = Vector3.Dot(direction, normal);
            if (MathF.Abs(denom) < 1e-4f)
                return;
            var t = Vector3.Dot(_grab - origin, normal) / denom;
            d = new Vec3(0, StageMath.Snap((origin + direction * t).Y - _grab.Y, step), 0);
        }
        else
        {
            if (StageGeometry.IntersectPlaneY(origin, direction, _planeY) is not { } hit)
                return;
            var raw = hit - _grab;
            d = new Vec3(StageMath.Snap(raw.X, step), 0, StageMath.Snap(raw.Z, step));
        }
        _moved = true;
        editor.MoveBy(d);
    }

    /// <inheritdoc/>
    public void Release(Point point)
    {
        if (_moved)
            state.Commit();
    }
}
