using System.Numerics;
using Lampion.Core.Model.Rig;

namespace Lampion.App.Rendering;

/// <summary>Fixture housings and their glowing lenses: battens, strobe panels, pars and moving heads on a yoke.</summary>
internal sealed class FixtureBodyPass(LitProgram lit, StageMeshes meshes)
{
    private static readonly Vector3 Housing = new(0.05f, 0.05f, 0.055f);
    private static readonly Vector3 LensFloor = new(0.01f);
    private const float YokeArm = 0.19f;

    /// <summary>Draws every fixture body; <see cref="SurfaceLighting.Begin"/> must have run this frame.</summary>
    /// <param name="fixtures">The scene's fixtures.</param>
    /// <param name="data">Per-fixture data parallel to <paramref name="fixtures"/>.</param>
    public void Draw(ReadOnlySpan<FixtureVisual> fixtures, ReadOnlySpan<FixtureDrawData> data)
    {
        for (var i = 0; i < fixtures.Length; i++)
            Draw(in fixtures[i], in data[i]);
    }

    private void Draw(in FixtureVisual f, in FixtureDrawData data)
    {
        var face = data.Facing;
        var at = Matrix4x4.CreateTranslation(f.Position);
        var emissive = f.Selected ? SetPiecePass.SelectGlow : Vector3.Zero;
        var lens = f.Light * 6f + LensFloor;
        lit.SetMaterial(Housing, 0.45f, 0.4f, emissive, SurfacePattern.Plain);

        switch (f.Kind)
        {
            case BeamKind.Bar:
                Panel(new Vector3(0.9f, 0.08f, 0.1f), new Vector3(0.86f, 0.05f, 0.01f), 0.051f, face, at, lens);
                return;
            case BeamKind.Strobe or BeamKind.Blinder:
                Panel(new Vector3(0.46f, 0.22f, 0.14f), new Vector3(0.42f, 0.18f, 0.01f), 0.071f, face, at, lens);
                return;
        }

        if (f.MovingHead)
            MovingHead(f.BeamDirection, face, at);
        else
            Can(f.Kind, face, at);
        lit.SetMaterial(Vector3.Zero, 1, 0, lens, SurfacePattern.Plain);
        var lensRadius = f.MovingHead ? 0.1f : f.Kind == BeamKind.Wash ? 0.14f : 0.1f;
        lit.SetModel(Matrix4x4.CreateScale(lensRadius, lensRadius, 0.01f) * Matrix4x4.CreateTranslation(0, 0, f.LensOffset - 0.005f) * face * at);
        meshes.Cylinder.Draw();
    }

    /// <summary>A flat box housing with an emissive face just in front of it (battens, strobes, blinders).</summary>
    private void Panel(Vector3 housing, Vector3 window, float windowZ, in Matrix4x4 face, in Matrix4x4 at, Vector3 lens)
    {
        lit.SetModel(Matrix4x4.CreateScale(housing) * face * at);
        meshes.Box.Draw();
        lit.SetMaterial(Vector3.Zero, 1, 0, lens, SurfacePattern.Plain);
        lit.SetModel(Matrix4x4.CreateScale(window) * Matrix4x4.CreateTranslation(0, 0, windowZ) * face * at);
        meshes.Box.Draw();
    }

    /// <summary>Base on the truss, a yoke turning with pan, and the head pointing along the beam.</summary>
    private void MovingHead(Vector3 beamDirection, in Matrix4x4 face, in Matrix4x4 at)
    {
        lit.SetModel(Matrix4x4.CreateScale(0.36f, 0.13f, 0.3f) * Matrix4x4.CreateTranslation(0, 0.26f, 0) * at);
        meshes.Box.Draw();
        var panYaw = MathF.Atan2(beamDirection.X, beamDirection.Z);
        var yoke = Matrix4x4.CreateRotationY(panYaw) * at;
        lit.SetModel(Matrix4x4.CreateScale(0.4f, 0.04f, 0.08f) * Matrix4x4.CreateTranslation(0, 0.18f, 0) * yoke);
        meshes.Box.Draw();
        var arm = Matrix4x4.CreateScale(0.035f, 0.22f, 0.08f);
        lit.SetModel(arm * Matrix4x4.CreateTranslation(-YokeArm, 0.07f, 0) * yoke);
        meshes.Box.Draw();
        lit.SetModel(arm * Matrix4x4.CreateTranslation(YokeArm, 0.07f, 0) * yoke);
        meshes.Box.Draw();
        lit.SetModel(Matrix4x4.CreateScale(0.13f, 0.13f, 0.34f) * Matrix4x4.CreateTranslation(0, 0, -0.17f) * face * at);
        meshes.Cylinder.Draw();
    }

    /// <summary>A par or wash can on a short hanging stem.</summary>
    private void Can(BeamKind kind, in Matrix4x4 face, in Matrix4x4 at)
    {
        var radius = kind == BeamKind.Wash ? 0.16f : 0.12f;
        lit.SetModel(Matrix4x4.CreateScale(0.03f, 0.2f, 0.03f) * Matrix4x4.CreateTranslation(0, 0.2f, 0) * at);
        meshes.Box.Draw();
        lit.SetModel(Matrix4x4.CreateScale(radius, radius, 0.3f) * Matrix4x4.CreateTranslation(0, 0, -0.15f) * face * at);
        meshes.Cylinder.Draw();
    }
}
