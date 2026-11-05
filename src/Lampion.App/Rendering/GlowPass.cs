using System.Numerics;

namespace Lampion.App.Rendering;

/// <summary>Lens glows and flares, billboarded toward the camera and added on top of everything.</summary>
internal sealed class GlowPass(GlowProgram glow, StageMeshes meshes)
{
    /// <summary>Draws a glow for every lit fixture; additive blending with depth writes off must already be set.</summary>
    /// <param name="fixtures">The scene's fixtures.</param>
    /// <param name="data">Per-fixture data parallel to <paramref name="fixtures"/>.</param>
    /// <param name="view">World-to-view matrix.</param>
    /// <param name="projection">View-to-clip matrix.</param>
    /// <param name="eye">Camera position, metres.</param>
    public void Draw(ReadOnlySpan<FixtureVisual> fixtures, ReadOnlySpan<FixtureDrawData> data, in Matrix4x4 view, in Matrix4x4 projection, Vector3 eye)
    {
        glow.Use();
        glow.SetCamera(in view, in projection);
        for (var i = 0; i < fixtures.Length; i++)
        {
            ref readonly var f = ref fixtures[i];
            if (f.Brightness < 0.01f)
                continue;
            var lens = data[i].Lens;
            var toCamera = Vector3.Normalize(eye - lens);
            var facing = MathF.Pow(MathF.Max(0, Vector3.Dot(toCamera, f.BeamDirection)), 6);
            glow.SetGlow(lens + toCamera * 0.05f, 0.35f + 0.9f * facing, facing, f.Light * 1.6f);
            meshes.Quad.Draw();
        }
    }
}
