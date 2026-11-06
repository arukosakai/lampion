using System.Numerics;

namespace Lampion.App.Controls.Stage3D;

/// <summary>The move gizmo on screen: where the selection sits and the end of each axis arrow, in pixels.</summary>
/// <param name="Origin">The selection's position.</param>
/// <param name="X">Tip of the X arrow (the origin when it would be behind the camera).</param>
/// <param name="Y">Tip of the Y arrow.</param>
/// <param name="Z">Tip of the Z arrow.</param>
/// <param name="Length">How many metres each arrow spans; it grows with distance so the arrows keep their size on screen.</param>
internal readonly record struct GizmoHandles(Vector2 Origin, Vector2 X, Vector2 Y, Vector2 Z, float Length)
{
    /// <summary>The tip of an axis: 0 X, 1 Y, anything else Z.</summary>
    public Vector2 Tip(int axis) =>
        axis switch
        {
            0 => X,
            1 => Y,
            _ => Z,
        };
}
