using Avalonia;

namespace Lampion.App.Controls.Stage3D;

/// <summary>Where a navigation-gizmo ball lands this frame.</summary>
/// <param name="Index">Into <see cref="GizmoBall.All"/>.</param>
/// <param name="Position">Centre in the gizmo control, pixels.</param>
/// <param name="Depth">-1 pointing away from the viewer .. 1 pointing at them.</param>
internal readonly record struct BallPlacement(int Index, Point Position, float Depth);
