using System.Numerics;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Memory;
using SharpGLTF.Scenes;

namespace Lampion.Core.Meshes;

/// <summary>Writes a <see cref="MeshModel"/> as one self-contained binary glTF (.glb), textures embedded, so a show's model is a single file.</summary>
public static class GlbWriter
{
    /// <summary>Writes <paramref name="model"/> to <paramref name="path"/>, replacing any file there.</summary>
    /// <exception cref="IOException">The file can't be written.</exception>
    public static void Write(MeshModel model, string path)
    {
        var mesh = new MeshBuilder<VertexPositionNormal, VertexTexture1>("model");
        for (var p = 0; p < model.Parts.Count; p++)
        {
            var part = model.Parts[p];
            var primitive = mesh.UsePrimitive(Material(part.Material, p));
            var v = part.Vertices;
            for (var i = 0; i + 2 < part.Indices.Length; i += 3)
                primitive.AddTriangle(Vertex(v, part.Indices[i]), Vertex(v, part.Indices[i + 1]), Vertex(v, part.Indices[i + 2]));
        }
        var scene = new SceneBuilder();
        scene.AddRigidMesh(mesh, Matrix4x4.Identity);
        scene.ToGltf2().SaveGLB(path);
    }

    private static MaterialBuilder Material(MeshMaterial source, int index)
    {
        var color = new Vector4(source.BaseColor, 1);
        var material = new MaterialBuilder(source.Name.Length > 0 ? source.Name : $"Material {index + 1}")
            .WithMetallicRoughnessShader()
            .WithMetallicRoughness(source.Metallic, source.Roughness)
            .WithEmissive(source.Emissive, 1);
        return source.BaseColorImage is { } image
            ? material.WithBaseColor(ImageBuilder.From(new MemoryImage(image), $"texture {index + 1}"), color)
            : material.WithBaseColor(color);
    }

    private static VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty> Vertex(float[] v, uint index)
    {
        var o = (int)index * MeshPart.FloatsPerVertex;
        return new(new VertexPositionNormal(v[o], v[o + 1], v[o + 2], v[o + 3], v[o + 4], v[o + 5]), new VertexTexture1(new Vector2(v[o + 6], v[o + 7])));
    }
}
