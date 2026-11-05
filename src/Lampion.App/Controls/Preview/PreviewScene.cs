using System.Numerics;
using Lampion.App.Rendering;
using Lampion.Core.Engine;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Stage;

namespace Lampion.App.Controls.Preview;

/// <summary>
/// Builds the fixture preview's scene from a profile and test DMX values, on the UI thread. Owns a copy of
/// the values, a single placed fixture and one scene it refills, so a frame allocates nothing unless the
/// fixture's look changed.
/// </summary>
internal sealed class PreviewScene
{
    private readonly Fixture _fixture = new();
    private readonly PreviewBeamCache _beams = new();
    private readonly SceneSnapshot _scene = new();
    private byte[] _values = [];
    private int _count;
    private bool _placed;

    /// <summary>Copies the test values (one per profile channel; missing ones read as absent). Allocates only to grow.</summary>
    public void SetValues(ReadOnlySpan<byte> values)
    {
        if (_values.Length < values.Length)
            _values = new byte[values.Length];
        values.CopyTo(_values);
        _count = values.Length;
    }

    /// <summary>The scene for this frame. The same instance is refilled on every call.</summary>
    /// <param name="profile">The profile to draw.</param>
    /// <param name="time">Seconds since the preview started; drives strobing and haze drift.</param>
    /// <param name="aimTarget">Where to draw the aim marker, or null for none.</param>
    public SceneSnapshot Build(FixtureProfile profile, float time, Vector3? aimTarget)
    {
        Place(profile);
        var emitted = DmxMapper.Decode(_fixture, profile, Values);
        var direction = StageMath.BeamDirection(_fixture, profile, emitted.Pan, emitted.Tilt).Normalized();
        var color = emitted.Color;
        var light = new Vector3(MathF.Pow((float)color.R, 2.2f), MathF.Pow((float)color.G, 2.2f), MathF.Pow((float)color.B, 2.2f));
        if (emitted.Strobe > 0.04 && (time * (1 + emitted.Strobe * 19)) % 1 > 0.18)
            light = Vector3.Zero;

        var key = new PreviewBeamKey(profile.Beam, direction, (float)profile.BeamAngle, light, profile.HasPanTilt);
        var fixtures = _beams.Get(in key);
        _scene.ClearFixtures();
        for (var i = 0; i < fixtures.Count; i++)
            _scene.AddFixture(fixtures[i]);
        _scene.SetObjects(PreviewSet.Visuals(key.OnFloor));
        _scene.SetEnvironment(0.55f, 0.3f, 0.35f, Vector3.Zero, time, null, aimTarget);
        return _scene;
    }

    /// <summary>
    /// The pan/tilt (0..1) that point the fixture at where a camera ray hits the set, nearest the current
    /// aim, or null when the ray misses, the head can't reach it, or no frame has been built yet.
    /// </summary>
    /// <param name="profile">The profile being drawn; it must have pan or tilt.</param>
    /// <param name="origin">Ray start, metres.</param>
    /// <param name="direction">Ray direction.</param>
    /// <param name="hit">Where the ray hit the set, when the result isn't null.</param>
    public (double Pan, double Tilt)? AimAt(FixtureProfile profile, Vector3 origin, Vector3 direction, out Vector3 hit)
    {
        hit = default;
        if (!_placed)
            return null;
        var from = new Vec3(origin.X, origin.Y, origin.Z);
        var dir = new Vec3(direction.X, direction.Y, direction.Z);
        if (StageMath.Raycast(PreviewSet.Objects(onFloor: false), from, dir, 60) is not { } distance)
            return null;
        var target = from + dir * distance;
        var current = DmxMapper.Decode(_fixture, profile, Values);
        if (StageMath.PanTiltToward(_fixture, profile, target, (current.Pan, current.Tilt)) is not { } aim)
            return null;
        hit = PreviewSet.ToVector(target);
        return aim;
    }

    private ReadOnlySpan<byte> Values => _values.AsSpan(0, _count);

    // Bars stand on the floor and uplight the wall; everything else hangs from the truss.
    private void Place(FixtureProfile profile)
    {
        var onFloor = profile.Beam == BeamKind.Bar;
        var position = onFloor ? PreviewSet.FloorPoint : PreviewSet.HangPoint;
        _fixture.Position = new Vec3(position.X, position.Y, position.Z);
        _fixture.Yaw = 180;
        _fixture.Pitch = onFloor ? 35 : profile.HasPanTilt ? -90 : -32;
        _placed = true;
    }
}
