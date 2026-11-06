using System.Numerics;
using Avalonia;
using Lampion.App.Editor;
using Lampion.Core.Model.Stage;

namespace Lampion.App.Controls.Stage3D;

/// <summary>Dragging one arrow of the move gizmo: the selection slides along that world axis only, snapped to the grid.</summary>
internal sealed class AxisDrag(EditorState state, SelectionEditor editor) : IStageDrag
{
    private Point _start;
    private Vector2 _screenAxis;
    private float _axisMetres;
    private bool _moved;

    /// <summary>The axis being dragged: 0 X, 1 Y, 2 Z.</summary>
    public int Axis { get; private set; }

    /// <summary>Starts a drag on <paramref name="axis"/>, whose arrow of <paramref name="axisMetres"/> metres spans <paramref name="screenAxis"/> pixels on screen.</summary>
    public void Begin(Point start, int axis, Vector2 screenAxis, float axisMetres)
    {
        (_start, Axis, _screenAxis, _axisMetres, _moved) = (start, axis, screenAxis, axisMetres, false);
        editor.CaptureOrigins();
    }

    /// <inheritdoc/>
    public void Move(Point point, Avalonia.Vector delta)
    {
        var moved = new Vector2((float)(point.X - _start.X), (float)(point.Y - _start.Y));
        var length = _screenAxis.Length();
        var metres = StageMath.Snap(Vector2.Dot(moved, _screenAxis / length) / length * _axisMetres, state.Project.Stage.GridStep);
        var d = Axis switch
        {
            0 => new Vec3(metres, 0, 0),
            1 => new Vec3(0, metres, 0),
            _ => new Vec3(0, 0, metres),
        };
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
