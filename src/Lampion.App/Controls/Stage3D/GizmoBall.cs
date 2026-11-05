using System.Numerics;
using Lampion.App.Rendering;

namespace Lampion.App.Controls.Stage3D;

/// <summary>One ball of the navigation gizmo.</summary>
/// <param name="Axis">World direction the ball stands for.</param>
/// <param name="View">The view a click on it applies: the camera on that side of the stage.</param>
/// <param name="Color">Index into the gizmo's X/Y/Z colours.</param>
/// <param name="Name">Letter drawn on it.</param>
internal readonly record struct GizmoBall(Vector3 Axis, ViewPreset View, int Color, string Name)
{
    /// <summary>The six balls: positive axes first, then their opposites.</summary>
    public static readonly GizmoBall[] All =
    [
        new(Vector3.UnitX, ViewPreset.Right, 0, "X"),
        new(Vector3.UnitY, ViewPreset.Top, 1, "Y"),
        new(Vector3.UnitZ, ViewPreset.Front, 2, "Z"),
        new(-Vector3.UnitX, ViewPreset.Left, 0, "-X"),
        new(-Vector3.UnitY, ViewPreset.Bottom, 1, "-Y"),
        new(-Vector3.UnitZ, ViewPreset.Back, 2, "-Z"),
    ];

    /// <summary>Whether the ball at this index of <see cref="All"/> is a positive axis (solid, with a line from the centre).</summary>
    public static bool IsPositive(int index) => index < 3;
}
