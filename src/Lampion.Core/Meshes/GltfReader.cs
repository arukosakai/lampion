using System.Numerics;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Schema2;

namespace Lampion.Core.Meshes;

/// <summary>
/// Reads glTF 2.0 (.gltf with its files, or self-contained .glb) through SharpGLTF: the default scene with every node
/// transform applied, grouped by material, with metallic-roughness parameters and PNG/JPEG base-colour textures.
/// </summary>
public static class GltfReader
{
    /// <summary>Reads <paramref name="path"/>.</summary>
    /// <exception cref="IOException">The file, or a buffer or image it points at, can't be read.</exception>
    /// <exception cref="InvalidDataException">The file isn't valid glTF.</exception>
    public static MeshModel Read(string path)
    {
        ModelRoot root;
        try
        {
            root = ModelRoot.Load(path);
        }
        catch (Exception ex) when (ex is not IOException)
        {
            throw new InvalidDataException($"{Path.GetFileName(path)} isn't a glTF file Lampion can read: {ex.Message}", ex);
        }

        var model = new MeshModel();
        var scene = root.DefaultScene ?? root.LogicalScenes.FirstOrDefault();
        if (scene is null)
            return model;
        var parts = new Dictionary<int, PartBuilder>();
        foreach (var triangle in scene.EvaluateTriangles<VertexPositionNormal, VertexTexture1>())
        {
            var key = triangle.Material?.LogicalIndex ?? -1;
            if (!parts.TryGetValue(key, out var part))
                parts[key] = part = new PartBuilder();
            part.Add(
                (triangle.A.Geometry.Position, triangle.A.Geometry.Normal, triangle.A.Material.TexCoord),
                (triangle.B.Geometry.Position, triangle.B.Geometry.Normal, triangle.B.Material.TexCoord),
                (triangle.C.Geometry.Position, triangle.C.Geometry.Normal, triangle.C.Material.TexCoord));
        }
        foreach (var (key, part) in parts)
            model.Parts.Add(part.Build(key >= 0 ? Material(root.LogicalMaterials[key], model.Warnings) : new MeshMaterial()));
        return model;
    }

    private static MeshMaterial Material(Material source, List<string> warnings)
    {
        var baseColor = source.FindChannel("BaseColor") ?? source.FindChannel("Diffuse");
        var color = baseColor?.Color ?? Vector4.One;
        var metallicRoughness = source.FindChannel("MetallicRoughness");
        var emissive = source.FindChannel("Emissive");
        return new MeshMaterial
        {
            Name = source.Name ?? "",
            BaseColor = new Vector3(color.X, color.Y, color.Z),
            Metallic = Factor(metallicRoughness, "MetallicFactor", 1),
            Roughness = Factor(metallicRoughness, "RoughnessFactor", 1),
            Emissive = emissive is { } e ? new Vector3(e.Color.X, e.Color.Y, e.Color.Z) : Vector3.Zero,
            BaseColorImage = Image(baseColor, source.Name, warnings),
        };
    }

    private static float Factor(MaterialChannel? channel, string name, float fallback)
    {
        if (channel is not { } c)
            return fallback;
        try
        {
            return Math.Clamp(c.GetFactor(name), 0, 1);
        }
        catch (ArgumentException)
        {
            return fallback;
        }
    }

    private static byte[]? Image(MaterialChannel? channel, string? material, List<string> warnings)
    {
        if (channel?.Texture?.PrimaryImage?.Content is not { IsValid: true } image)
            return null;
        var bytes = image.Content.ToArray();
        if (TextureFiles.IsPngOrJpeg(bytes))
            return bytes;
        warnings.Add($"The texture of material \"{material}\" is {image.FileExtension}, not PNG or JPEG; that surface is drawn in its plain colour.");
        return null;
    }

    private sealed class PartBuilder
    {
        private readonly List<float> _vertices = [];
        private readonly List<uint> _indices = [];
        private readonly Dictionary<(Vector3, Vector3, Vector2), uint> _shared = [];

        public void Add((Vector3 P, Vector3 N, Vector2 Uv) a, (Vector3 P, Vector3 N, Vector2 Uv) b, (Vector3 P, Vector3 N, Vector2 Uv) c)
        {
            // Primitives without normals come through with zero ones; give those faces a flat normal.
            var flat = Vector3.Cross(b.P - a.P, c.P - a.P);
            flat = flat.LengthSquared() > 0 ? Vector3.Normalize(flat) : Vector3.UnitY;
            _indices.Add(Vertex(a, flat));
            _indices.Add(Vertex(b, flat));
            _indices.Add(Vertex(c, flat));
        }

        public MeshPart Build(MeshMaterial material) => new(material, [.. _vertices], [.. _indices]);

        private uint Vertex((Vector3 P, Vector3 N, Vector2 Uv) v, Vector3 flat)
        {
            var normal = v.N.LengthSquared() > 1e-8f ? Vector3.Normalize(v.N) : flat;
            var key = (v.P, normal, v.Uv);
            if (_shared.TryGetValue(key, out var id))
                return id;
            id = (uint)(_vertices.Count / MeshPart.FloatsPerVertex);
            _vertices.AddRange([v.P.X, v.P.Y, v.P.Z, normal.X, normal.Y, normal.Z, v.Uv.X, v.Uv.Y]);
            _shared[key] = id;
            return id;
        }
    }
}
