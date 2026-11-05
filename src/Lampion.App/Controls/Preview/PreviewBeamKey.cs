using System.Numerics;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Stage;

namespace Lampion.App.Controls.Preview;

/// <summary>Everything the preview fixture's drawn look depends on; equal keys draw identically.</summary>
/// <param name="Kind">How the fixture is drawn; bars stand on the floor, the rest hang.</param>
/// <param name="Direction">Unit beam direction.</param>
/// <param name="BeamAngle">Beam angle, degrees.</param>
/// <param name="Light">Linear-light colour, zero while a strobe is dark.</param>
/// <param name="MovingHead">Whether to draw a yoke and head.</param>
internal readonly record struct PreviewBeamKey(BeamKind Kind, Vec3 Direction, float BeamAngle, Vector3 Light, bool MovingHead)
{
    /// <summary>Whether the fixture stands on the floor rather than hanging from the truss.</summary>
    public bool OnFloor => Kind == BeamKind.Bar;
}
