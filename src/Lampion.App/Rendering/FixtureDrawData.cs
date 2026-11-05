using System.Numerics;

namespace Lampion.App.Rendering;

/// <summary>
/// What several passes need from one fixture, worked out once per frame instead of once per pass.
/// Lives in a renderer-owned array parallel to <see cref="SceneSnapshot.Fixtures"/>; read it by <c>ref readonly</c>.
/// </summary>
internal readonly struct FixtureDrawData
{
    /// <summary>Works out the fixture's orientation and beam power.</summary>
    public FixtureDrawData(in FixtureVisual fixture)
    {
        Facing = StageTransforms.Facing(fixture.BeamDirection);
        Power = StageTransforms.BeamPower(fixture.BeamAngle);
        Lens = fixture.Lens;
    }

    /// <summary><see cref="StageTransforms.Facing"/> of the beam direction.</summary>
    public Matrix4x4 Facing { get; }

    /// <summary><see cref="StageTransforms.BeamPower"/> of the beam angle.</summary>
    public float Power { get; }

    /// <summary><see cref="FixtureVisual.Lens"/>.</summary>
    public Vector3 Lens { get; }
}
