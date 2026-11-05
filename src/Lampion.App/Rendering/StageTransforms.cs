using System.Numerics;

namespace Lampion.App.Rendering;

/// <summary>Model-matrix helpers shared by the draw passes.</summary>
internal static class StageTransforms
{
    /// <summary>Rotation taking +Z onto <paramref name="forward"/>, keeping the local up as close to world up as it can.</summary>
    public static Matrix4x4 Facing(Vector3 forward)
    {
        var z = Vector3.Normalize(forward);
        var helper = MathF.Abs(z.Y) > 0.98f ? Vector3.UnitX : Vector3.UnitY;
        var x = Vector3.Normalize(Vector3.Cross(helper, z));
        var y = Vector3.Cross(z, x);
        return new Matrix4x4(x.X, x.Y, x.Z, 0, y.X, y.Y, y.Z, 0, z.X, z.Y, z.Z, 0, 0, 0, 0, 1);
    }

    /// <summary>
    /// How bright a fixture's light lands, as a multiplier on its colour. The same lamp spread over a
    /// narrower beam lands brighter; this keeps spots punchy and washes soft.
    /// </summary>
    /// <param name="beamAngle">Full cone angle in degrees; clamped to 4..120.</param>
    public static float BeamPower(float beamAngle) => 22f * MathF.Pow(25f / Math.Clamp(beamAngle, 4, 120), 0.9f);
}
