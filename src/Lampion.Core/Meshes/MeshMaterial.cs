using System.Numerics;

namespace Lampion.Core.Meshes;

/// <summary>A surface of an imported model, reduced to what the stage renderer draws: a metallic-roughness material.</summary>
public sealed class MeshMaterial
{
    /// <summary>Name from the source file, for messages; may be empty.</summary>
    public string Name { get; init; } = "";

    /// <summary>Base colour, linear RGB 0..1; multiplies <see cref="BaseColorImage"/> when there is one.</summary>
    public Vector3 BaseColor { get; init; } = new(0.8f);

    /// <summary>0 mirror .. 1 matte.</summary>
    public float Roughness { get; init; } = 0.6f;

    /// <summary>0 dielectric .. 1 metal.</summary>
    public float Metallic { get; init; }

    /// <summary>Light the surface gives off itself, linear RGB.</summary>
    public Vector3 Emissive { get; init; }

    /// <summary>The colour texture as an encoded PNG or JPEG file, sRGB; null for none. UV (0,0) is the image's top-left.</summary>
    public byte[]? BaseColorImage { get; init; }
}
