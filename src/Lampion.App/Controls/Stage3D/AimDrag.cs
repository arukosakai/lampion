using Avalonia;
using Lampion.App.Editor;

namespace Lampion.App.Controls.Stage3D;

/// <summary>Aim tool: the selected fixtures follow the pointer across the stage; one undo step on release.</summary>
internal sealed class AimDrag(StageView view, EditorState state, SelectionEditor editor) : IStageDrag
{
    /// <summary>Aims at the press point and starts following the pointer.</summary>
    public void Begin(Point start) => AimAt(start);

    /// <inheritdoc/>
    public void Move(Point point, Vector delta) => AimAt(point);

    /// <inheritdoc/>
    public void Release(Point point) => state.Commit();

    private void AimAt(Point point)
    {
        var (origin, direction) = view.RayAt(point);
        editor.AimAlong(origin, direction);
    }
}
