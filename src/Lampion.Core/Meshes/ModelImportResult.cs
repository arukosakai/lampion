using Lampion.Core.Model.Stage;

namespace Lampion.Core.Meshes;

/// <summary>What <see cref="ModelAssets.Import"/> produced.</summary>
/// <param name="Model">Path of the new .glb relative to the assets folder, for <see cref="StageObject.Model"/>.</param>
/// <param name="Size">The model's natural width, height and depth in metres, for <see cref="StageObject.Size"/>.</param>
/// <param name="Triangles">How many triangles it has, so the user can tell a heavy model.</param>
/// <param name="Warnings">Problems that didn't stop the import, in plain words.</param>
/// <param name="UnitNote">How it was rescaled from centimetres or millimetres, or null when it was already in metres.</param>
public sealed record ModelImportResult(string Model, Vec3 Size, int Triangles, IReadOnlyList<string> Warnings, string? UnitNote);
