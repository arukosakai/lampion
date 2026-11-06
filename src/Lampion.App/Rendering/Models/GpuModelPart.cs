using Lampion.Core.Meshes;

namespace Lampion.App.Rendering.Models;

/// <summary>One material's worth of a <see cref="GpuModel"/>.</summary>
/// <param name="Mesh">Its triangles.</param>
/// <param name="Material">How they look.</param>
/// <param name="Texture">GL name of the colour texture, or 0 for none.</param>
internal readonly record struct GpuModelPart(GpuMesh Mesh, MeshMaterial Material, uint Texture);
