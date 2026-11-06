using System.Numerics;

namespace Lampion.Core.Meshes;

/// <summary>An imported 3D model in Lampion's terms: metres, Y up, front facing +Z, split into parts by material.</summary>
public sealed class MeshModel
{
    /// <summary>The model's surfaces.</summary>
    public List<MeshPart> Parts { get; } = [];

    /// <summary>Problems that didn't stop the import (a missing texture, an unsupported primitive), in plain words.</summary>
    public List<string> Warnings { get; } = [];

    /// <summary>Total triangles over all parts.</summary>
    public int TriangleCount => Parts.Sum(p => p.Indices.Length / 3);

    /// <summary>Smallest and largest corner of the box around every vertex; both zero for an empty model.</summary>
    public (Vector3 Min, Vector3 Max) Bounds()
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var part in Parts)
            for (var i = 0; i < part.Vertices.Length; i += MeshPart.FloatsPerVertex)
            {
                var p = new Vector3(part.Vertices[i], part.Vertices[i + 1], part.Vertices[i + 2]);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
        return min.X <= max.X ? (min, max) : (Vector3.Zero, Vector3.Zero);
    }

    /// <summary>Width, height and depth of <see cref="Bounds"/>, metres.</summary>
    public Vector3 Size()
    {
        var (min, max) = Bounds();
        return max - min;
    }

    /// <summary>Scales every vertex by <paramref name="scale"/> and moves the model by <paramref name="offset"/> afterwards, in place.</summary>
    public void Transform(float scale, Vector3 offset)
    {
        foreach (var part in Parts)
            for (var i = 0; i < part.Vertices.Length; i += MeshPart.FloatsPerVertex)
            {
                part.Vertices[i] = part.Vertices[i] * scale + offset.X;
                part.Vertices[i + 1] = part.Vertices[i + 1] * scale + offset.Y;
                part.Vertices[i + 2] = part.Vertices[i + 2] * scale + offset.Z;
            }
    }
}
