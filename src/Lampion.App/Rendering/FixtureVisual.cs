using System.Numerics;
using Lampion.Core.Model.Rig;

namespace Lampion.App.Rendering;

/// <summary>What one fixture looks like this frame, already decoded from its DMX output.</summary>
/// <param name="Id">The fixture's id, for matching a visual back to the model.</param>
/// <param name="Kind">Which body and beam to draw.</param>
/// <param name="Position">Fixture centre, metres.</param>
/// <param name="BeamDirection">Unit vector the beam travels along.</param>
/// <param name="BeamAngle">Full cone angle in degrees.</param>
/// <param name="Light">Emitted light, linear RGB (0..1 per channel), zero while a strobe is dark.</param>
/// <param name="BeamLength">Metres from the lens to where the beam lands (capped at 30).</param>
/// <param name="MovingHead">Whether to draw a base and yoke that follow pan and tilt.</param>
/// <param name="Selected">Whether to draw the selection glow.</param>
/// <param name="Mount">Body rotation around <see cref="Position"/> from the fixture's roll; the beam is already rotated.</param>
/// <remarks>About 86 bytes: pass it by <c>in</c> or read it from a span rather than copying it around.</remarks>
internal readonly record struct FixtureVisual(
    string Id,
    BeamKind Kind,
    Vector3 Position,
    Vector3 BeamDirection,
    float BeamAngle,
    Vector3 Light,
    float BeamLength,
    bool MovingHead,
    bool Selected,
    Quaternion Mount
)
{
    /// <summary>Front of the lens, where the beam starts.</summary>
    public Vector3 Lens => Position + BeamDirection * LensOffset;

    /// <summary>Metres from <see cref="Position"/> to the front of the lens, along the beam.</summary>
    public float LensOffset => LensOffsetOf(Kind);

    /// <summary>Brightest channel of <see cref="Light"/>.</summary>
    public float Brightness => MathF.Max(Light.X, MathF.Max(Light.Y, Light.Z));

    /// <summary>Metres from a fixture's centre to the front of its lens, by body kind.</summary>
    public static float LensOffsetOf(BeamKind kind) =>
        kind switch
        {
            BeamKind.Bar => 0.06f,
            BeamKind.Strobe or BeamKind.Blinder => 0.08f,
            BeamKind.Spot => 0.18f,
            _ => 0.16f,
        };
}
