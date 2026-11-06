using System.Numerics;
using Lampion.App.Rendering.Models;
using Lampion.Core.Model.Stage;
using Silk.NET.OpenGL;

namespace Lampion.App.Rendering;

/// <summary>Trusses, risers, walls, LED screens and imported models, lit by <see cref="SurfaceLighting"/>.</summary>
internal sealed class SetPiecePass(GL gl, LitProgram lit, StageMeshes meshes, ModelCache models)
{
    /// <summary>Emissive tint that marks a selected object or fixture.</summary>
    public static readonly Vector3 SelectGlow = new(0.05f, 0.22f, 0.55f);

    private static readonly Vector3 ScreenFrame = new(0.03f);

    /// <summary>Draws every set piece; <see cref="SurfaceLighting.Begin"/> must have run this frame.</summary>
    public void Draw(ReadOnlySpan<ObjectVisual> objects)
    {
        for (var i = 0; i < objects.Length; i++)
            Draw(in objects[i]);
    }

    private void Draw(in ObjectVisual obj)
    {
        var place = Matrix4x4.CreateFromQuaternion(obj.Rotation) * Matrix4x4.CreateTranslation(obj.Position);
        var emissive = SelectGlow * obj.Highlight;
        switch (obj.Kind)
        {
            case StageObjectKind.Truss:
                lit.SetMaterial(obj.Color, 0.35f, 0.9f, emissive, SurfacePattern.BrushedMetal);
                lit.SetModel(in place);
                meshes.TrussMesh(obj.Size).Draw();
                break;
            case StageObjectKind.Wall:
                lit.SetMaterial(obj.Color, 0.9f, 0, emissive, SurfacePattern.Drape);
                lit.SetModel(Matrix4x4.CreateScale(obj.Size) * place);
                meshes.Box.Draw();
                break;
            case StageObjectKind.Screen:
                lit.SetMaterial(ScreenFrame, 0.5f, 0.3f, emissive, SurfacePattern.Plain);
                lit.SetModel(Matrix4x4.CreateScale(obj.Size) * place);
                meshes.Box.Draw();
                lit.SetMaterial(Vector3.One, 1, 0, Vector3.Zero, SurfacePattern.Video);
                var face = Matrix4x4.CreateScale(obj.Size.X * 0.97f, obj.Size.Y * 0.95f, 1) * Matrix4x4.CreateTranslation(0, 0, obj.Size.Z / 2 + 0.003f);
                lit.SetModel(face * place);
                meshes.Quad.Draw();
                break;
            case StageObjectKind.Model when obj.ModelPath is { } path && models.TryGet(path, out var model):
                DrawModel(model, obj.Size, in place, emissive);
                break;
            case StageObjectKind.Model:
                // Still loading, or the file is gone: a plain box shows where it stands and how big it is.
                lit.SetMaterial(obj.Color, 0.8f, 0, emissive, SurfacePattern.Plain);
                lit.SetModel(Matrix4x4.CreateScale(obj.Size) * place);
                meshes.Box.Draw();
                break;
            default:
                lit.SetMaterial(obj.Color, 0.5f, 0, emissive, SurfacePattern.Deck);
                lit.SetModel(Matrix4x4.CreateScale(obj.Size) * place);
                meshes.Box.Draw();
                break;
        }
    }

    // The stored model is centred on its box, so stretching it to the object's size and placing it like a box fits it exactly.
    private void DrawModel(GpuModel model, Vector3 size, in Matrix4x4 place, Vector3 emissive)
    {
        lit.SetModel(Matrix4x4.CreateScale(size / model.Size) * place);
        foreach (ref readonly var part in model.Parts)
        {
            var m = part.Material;
            lit.SetMaterial(m.BaseColor, m.Roughness, m.Metallic, m.Emissive + emissive, SurfacePattern.Plain);
            var textured = part.Texture != 0;
            lit.SetAlbedoMap(textured);
            if (textured)
            {
                gl.ActiveTexture(TextureUnit.Texture0 + SurfaceLighting.AlbedoMapUnit);
                gl.BindTexture(TextureTarget.Texture2D, part.Texture);
            }
            part.Mesh.Draw();
        }
        lit.SetAlbedoMap(false);
    }
}
