using System.Numerics;
using Lampion.App.Rendering;
using Lampion.Core.Model.Rig;

namespace Lampion.App.Controls.Stage3D;

internal sealed partial class StageSceneBuilder
{
    /// <summary>A patched fixture with everything about it that only changes with an edit, resolved once.</summary>
    private readonly struct RigEntry
    {
        public RigEntry(Fixture fixture, FixtureProfile profile, bool selected)
        {
            Fixture = fixture;
            Profile = profile;
            Kind = profile.Beam;
            BeamAngle = (float)profile.BeamAngle;
            MovingHead = profile.HasPanTilt;
            Selected = selected;
            LensOffset = FixtureVisual.LensOffsetOf(Kind);
            Mount = StageGeometry.Mount(fixture);
            // Wider beams light more of the set, so they spill more back into the room.
            BounceWeight = MathF.Sqrt(Math.Clamp(BeamAngle, 5, 120) / 25f);
        }

        public Fixture Fixture { get; }

        public FixtureProfile Profile { get; }

        public BeamKind Kind { get; }

        public float BeamAngle { get; }

        public bool MovingHead { get; }

        public bool Selected { get; }

        public float LensOffset { get; }

        public float BounceWeight { get; }

        public Quaternion Mount { get; }
    }
}
