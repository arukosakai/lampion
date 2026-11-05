using System.Numerics;

namespace Lampion.App.Rendering;

/// <summary>Volumetric beams through the haze, added on top of the lit picture.</summary>
internal sealed class BeamPass(BeamProgram beam, StageMeshes meshes)
{
    // Wide washes spread the same light thin; this keeps them from fogging out the whole view.
    private const float Scatter = 0.12f;

    /// <summary>Draws every lit beam; additive blending with depth writes off must already be set.</summary>
    /// <param name="scene">The frame's scene; nothing is drawn without haze.</param>
    /// <param name="data">Per-fixture data parallel to <see cref="SceneSnapshot.Fixtures"/>.</param>
    /// <param name="view">World-to-view matrix.</param>
    /// <param name="projection">View-to-clip matrix.</param>
    /// <param name="eye">Camera position, metres.</param>
    /// <param name="hazeSteps">Ray-march steps per beam.</param>
    public void Draw(SceneSnapshot scene, ReadOnlySpan<FixtureDrawData> data, in Matrix4x4 view, in Matrix4x4 projection, Vector3 eye, int hazeSteps)
    {
        if (scene.Haze <= 0.001f)
            return;
        beam.Use();
        beam.SetFrame(in view, in projection, eye, scene.Haze, scene.Time, hazeSteps);
        var fixtures = scene.Fixtures;
        for (var i = 0; i < fixtures.Length; i++)
        {
            ref readonly var f = ref fixtures[i];
            if (f.Brightness < 0.01f || f.BeamLength < 0.05f)
                continue;
            ref readonly var d = ref data[i];
            var half = Math.Clamp(f.BeamAngle, 2, 150) * 0.5f * MathF.PI / 180;
            var radius = f.BeamLength * MathF.Tan(half);
            var model = Matrix4x4.CreateScale(radius * 1.04f, radius * 1.04f, f.BeamLength) * d.Facing * Matrix4x4.CreateTranslation(d.Lens);
            beam.SetBeam(in model, d.Lens, f.BeamDirection, MathF.Cos(half), f.BeamLength, f.Light * d.Power * Scatter);
            meshes.Cone.Draw();
        }
    }
}
