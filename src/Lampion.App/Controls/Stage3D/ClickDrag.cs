using Avalonia;
using Lampion.App.Editor;

namespace Lampion.App.Controls.Stage3D;

/// <summary>A left press on empty stage: a click deselects, a drag past a few pixels orbits instead.</summary>
internal sealed class ClickDrag(StageView view, EditorState state) : IStageDrag
{
    private const double ClickSlop = 4;

    private Point _start;

    /// <summary>Starts a press at <paramref name="start"/>.</summary>
    public void Begin(Point start) => _start = start;

    /// <inheritdoc/>
    public void Move(Point point, Vector delta)
    {
        if (StageGeometry.Distance(point, _start) < ClickSlop)
            return;
        view.Camera.Orbit((float)delta.X, (float)delta.Y);
    }

    /// <inheritdoc/>
    public void Release(Point point)
    {
        if (StageGeometry.Distance(point, _start) < ClickSlop)
            state.Deselect();
    }
}
