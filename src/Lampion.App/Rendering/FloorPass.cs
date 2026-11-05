using System.Numerics;

namespace Lampion.App.Rendering;

/// <summary>The house floor: a 120 m polished-concrete quad carrying the builder's metre grid.</summary>
internal sealed class FloorPass(LitProgram lit, StageMeshes meshes)
{
    private static readonly Vector3 Concrete = new(0.17f, 0.17f, 0.18f);

    // A hair below zero so platform bottoms never fight it.
    private static readonly Matrix4x4 Placement =
        Matrix4x4.CreateScale(120, 120, 1) * Matrix4x4.CreateRotationX(-MathF.PI / 2) * Matrix4x4.CreateTranslation(0, -0.002f, 0);

    /// <summary>Draws the floor; <see cref="SurfaceLighting.Begin"/> must have run this frame.</summary>
    /// <param name="gridStrength">How strongly the metre grid shows, 0..1.</param>
    public void Draw(float gridStrength)
    {
        lit.SetGrid(gridStrength);
        lit.SetMaterial(Concrete, 0.3f, 0, Vector3.Zero, SurfacePattern.HouseFloor);
        lit.SetModel(in Placement);
        meshes.Quad.Draw();
    }
}
