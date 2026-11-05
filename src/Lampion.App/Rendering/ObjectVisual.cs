using System.Numerics;
using Lampion.Core.Model.Stage;

namespace Lampion.App.Rendering;

/// <summary>How one set piece is drawn.</summary>
/// <param name="Id">The object's id, for matching a visual back to the model.</param>
/// <param name="Kind">Which mesh and material to use.</param>
/// <param name="Position">Box centre, metres.</param>
/// <param name="Size">Width, height and depth, metres.</param>
/// <param name="Yaw">Degrees around Y.</param>
/// <param name="Color">Surface colour, linear RGB 0..1.</param>
/// <param name="Selected">Whether to draw the selection glow.</param>
internal readonly record struct ObjectVisual(string Id, StageObjectKind Kind, Vector3 Position, Vector3 Size, float Yaw, Vector3 Color, bool Selected);
