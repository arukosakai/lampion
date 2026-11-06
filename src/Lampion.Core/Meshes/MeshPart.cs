namespace Lampion.Core.Meshes;

/// <summary>Triangles of an imported model that share one material.</summary>
/// <param name="Material">How the triangles look.</param>
/// <param name="Vertices">Interleaved position xyz, normal xyz, uv; <see cref="FloatsPerVertex"/> floats each, metres.</param>
/// <param name="Indices">Three vertex indices per triangle, counter-clockwise from the front.</param>
public sealed record MeshPart(MeshMaterial Material, float[] Vertices, uint[] Indices)
{
    /// <summary>Floats per vertex in <see cref="Vertices"/>; the same layout the stage renderer uploads.</summary>
    public const int FloatsPerVertex = 8;

    /// <summary>Number of vertices.</summary>
    public int VertexCount => Vertices.Length / FloatsPerVertex;
}
