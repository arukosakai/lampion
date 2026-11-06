using System.Numerics;
using Lampion.Core.Model.Stage;

namespace Lampion.App.Rendering;

/// <summary>How one set piece is drawn.</summary>
/// <param name="Id">The object's id, for matching a visual back to the model.</param>
/// <param name="Kind">Which mesh and material to use.</param>
/// <param name="Position">Box centre, metres.</param>
/// <param name="Size">Width, height and depth, metres.</param>
/// <param name="Rotation">Orientation around the centre.</param>
/// <param name="Color">Surface colour, linear RGB 0..1.</param>
/// <param name="Highlight">Selection glow strength: 1 for the selected piece, less for the rest of its structure, 0 for none.</param>
/// <param name="ModelPath">For imported models, the .glb file to draw; null for other kinds, or when the show is unsaved and it can't be found.</param>
internal readonly record struct ObjectVisual(string Id, StageObjectKind Kind, Vector3 Position, Vector3 Size, Quaternion Rotation, Vector3 Color, float Highlight, string? ModelPath = null);
