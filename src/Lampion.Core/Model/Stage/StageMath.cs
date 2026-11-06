using Lampion.Core.Model.Rig;

namespace Lampion.Core.Model.Stage;

/// <summary>
/// Geometry for the stage builder and the preview. Everything is in metres and degrees. Stateless and
/// thread-safe; the ray tests are allocation-free so the preview can run them per fixture per frame.
/// </summary>
public static class StageMath
{
    private const double Deg = Math.PI / 180;

    /// <summary>Unit vector for a yaw/pitch aim in degrees (yaw 0 faces the audience, pitch -90 points down).</summary>
    public static Vec3 Direction(double yawDegrees, double pitchDegrees)
    {
        var yaw = yawDegrees * Deg;
        var pitch = pitchDegrees * Deg;
        return new Vec3(Math.Sin(yaw) * Math.Cos(pitch), Math.Sin(pitch), Math.Cos(yaw) * Math.Cos(pitch));
    }

    /// <summary>
    /// Where the beam points once a moving head's pan/tilt (0..1, centre 0.5) is applied to its rigging aim.
    /// Fixtures without pan/tilt (or a null profile) just use their rigging aim.
    /// </summary>
    public static Vec3 BeamDirection(Fixture fixture, FixtureProfile? profile, double pan, double tilt)
    {
        if (profile is null || !profile.HasPanTilt)
            return Direction(fixture.Yaw, fixture.Pitch);
        var unrolled = Direction(fixture.Yaw + (pan - 0.5) * profile.PanRange, fixture.Pitch + (tilt - 0.5) * profile.TiltRange);
        return fixture.Roll == 0 ? unrolled : Rotate(unrolled, Direction(fixture.Yaw, fixture.Pitch), fixture.Roll);
    }

    /// <summary><paramref name="v"/> turned <paramref name="degrees"/> around the unit <paramref name="axis"/> (right-handed, Rodrigues).</summary>
    public static Vec3 Rotate(Vec3 v, Vec3 axis, double degrees)
    {
        var cos = Math.Cos(degrees * Deg);
        var sin = Math.Sin(degrees * Deg);
        return v * cos + Vec3.Cross(axis, v) * sin + axis * (Vec3.Dot(axis, v) * (1 - cos));
    }

    /// <summary>Yaw and pitch in degrees that aim from <paramref name="from"/> at <paramref name="target"/>; straight down when they coincide.</summary>
    public static (double Yaw, double Pitch) AimAt(Vec3 from, Vec3 target)
    {
        var d = target - from;
        var flat = Math.Sqrt(d.X * d.X + d.Z * d.Z);
        if (flat < 1e-9 && Math.Abs(d.Y) < 1e-9)
            return (0, -90);
        return (Math.Atan2(d.X, d.Z) / Deg, Math.Atan2(d.Y, flat) / Deg);
    }

    /// <summary>
    /// Pan/tilt (0..1) that point a moving head at <paramref name="target"/>, or null when it can't reach.
    /// Several settings hit the same spot (flip the head, go round once more); the one nearest
    /// <paramref name="near"/> wins, so dragging a target never makes the head swing all the way round.
    /// </summary>
    public static (double Pan, double Tilt)? PanTiltToward(Fixture fixture, FixtureProfile profile, Vec3 target, (double Pan, double Tilt) near)
    {
        if (profile.PanRange <= 0 || profile.TiltRange <= 0)
            return null;
        var toward = target - fixture.Position;
        if (fixture.Roll != 0)
            toward = Rotate(toward, Direction(fixture.Yaw, fixture.Pitch), -fixture.Roll);
        var (yaw, pitch) = AimAt(new Vec3(0, 0, 0), toward);
        (double Pan, double Tilt)? best = null;
        var bestDistance = double.MaxValue;
        for (var flip = 0; flip < 2; flip++)
        {
            // The second pass is the same direction seen from a head flipped over the top.
            var y = flip == 0 ? yaw : yaw + 180;
            var p = flip == 0 ? pitch : -180 - pitch;
            for (var turnsYaw = -2; turnsYaw <= 2; turnsYaw++)
                for (var turnsPitch = -1; turnsPitch <= 1; turnsPitch++)
                {
                    var pan = (y + 360 * turnsYaw - fixture.Yaw) / profile.PanRange + 0.5;
                    var tilt = (p + 360 * turnsPitch - fixture.Pitch) / profile.TiltRange + 0.5;
                    if (pan < -1e-9 || pan > 1 + 1e-9 || tilt < -1e-9 || tilt > 1 + 1e-9)
                        continue;
                    var distance = Math.Abs(pan - near.Pan) * profile.PanRange + Math.Abs(tilt - near.Tilt) * profile.TiltRange;
                    if (distance < bestDistance)
                        (best, bestDistance) = ((Math.Clamp(pan, 0, 1), Math.Clamp(tilt, 0, 1)), distance);
                }
        }
        return best;
    }

    /// <summary>Sets the fixture's yaw and pitch (rounded to 0.01°) so it aims at <paramref name="target"/>.</summary>
    public static void AimAt(Fixture fixture, Vec3 target)
    {
        var (yaw, pitch) = AimAt(fixture.Position, target);
        fixture.Yaw = Math.Round(yaw, 2);
        fixture.Pitch = Math.Round(pitch, 2);
    }

    /// <summary>A point in an object's local frame (metres from its centre) moved into the world.</summary>
    public static Vec3 LocalToWorld(StageObject obj, Vec3 local) => obj.Position + BasisOf(obj).ToWorld(local);

    /// <summary>How far the object reaches above and below its centre once rotated, in metres.</summary>
    public static double HalfHeight(StageObject obj) => HalfExtents(obj).Y;

    /// <summary>Half the size of the world-axis box that holds the object once rotated, in metres from its centre.</summary>
    public static Vec3 HalfExtents(StageObject obj)
    {
        var basis = BasisOf(obj);
        var half = obj.Size * 0.5;
        return new Vec3(
            Math.Abs(basis.X.X) * half.X + Math.Abs(basis.Y.X) * half.Y + Math.Abs(basis.Z.X) * half.Z,
            Math.Abs(basis.X.Y) * half.X + Math.Abs(basis.Y.Y) * half.Y + Math.Abs(basis.Z.Y) * half.Z,
            Math.Abs(basis.X.Z) * half.X + Math.Abs(basis.Y.Z) * half.Y + Math.Abs(basis.Z.Z) * half.Z);
    }

    private static Basis BasisOf(StageObject obj) => Basis.FromYawPitchRoll(obj.Yaw, obj.Pitch, obj.Roll);

    /// <summary>
    /// Spreads fixtures evenly along a truss with half a gap at each end, like on a real bar. A lying truss
    /// gets them <paramref name="drop"/> metres below its bottom chord; an upright tower (more than ~45° up)
    /// gets them clamped that far out on whichever face looks most toward the audience.
    /// </summary>
    public static void DistributeAlong(StageObject truss, IReadOnlyList<Fixture> fixtures, double drop = 0.35)
    {
        if (fixtures.Count == 0)
            return;
        var basis = BasisOf(truss);
        var upright = Math.Abs(basis.X.Y) > Math.Sqrt(0.5);
        var outward = upright ? AudienceFace(basis, truss.Size, drop) : new Vec3(0, -truss.Size.Y / 2 - drop, 0);
        var length = truss.Size.X;
        var gap = length / fixtures.Count;
        for (var i = 0; i < fixtures.Count; i++)
        {
            var along = -length / 2 + gap * (i + 0.5);
            fixtures[i].Position = Round(truss.Position + basis.ToWorld(outward with { X = along }));
        }
    }

    /// <summary><paramref name="clearance"/> metres out from whichever side face (±local Y or ±Z) looks most toward the audience, in local metres.</summary>
    private static Vec3 AudienceFace(in Basis basis, Vec3 size, double clearance)
    {
        var yFacing = basis.Y.Z;
        var zFacing = basis.Z.Z;
        return Math.Abs(yFacing) > Math.Abs(zFacing)
            ? new Vec3(0, (yFacing < 0 ? -1 : 1) * (size.Y / 2 + clearance), 0)
            : new Vec3(0, 0, (zFacing < 0 ? -1 : 1) * (size.Z / 2 + clearance));
    }

    /// <summary><paramref name="value"/> rounded to the nearest multiple of <paramref name="step"/>; unchanged when the step is not positive.</summary>
    public static double Snap(double value, double step) => step <= 0 ? value : Math.Round(value / step) * step;

    /// <summary>Each component snapped to <paramref name="step"/>.</summary>
    public static Vec3 Snap(Vec3 v, double step) => new(Snap(v.X, step), Snap(v.Y, step), Snap(v.Z, step));

    /// <summary>Each component rounded to <paramref name="digits"/> decimals (millimetres by default).</summary>
    public static Vec3 Round(Vec3 v, int digits = 3) => new(Math.Round(v.X, digits), Math.Round(v.Y, digits), Math.Round(v.Z, digits));

    /// <summary>
    /// Distance along the ray to the first stage surface hit (objects other than truss, and the floor at
    /// y = 0), or null when nothing is hit. Allocation-free.
    /// </summary>
    /// <param name="objects">Set pieces to test, in any order.</param>
    /// <param name="origin">Ray start, metres.</param>
    /// <param name="direction">Ray direction; distances are in multiples of its length.</param>
    /// <param name="maxDistance">Furthest floor hit that counts; object hits are not limited by it.</param>
    public static double? Raycast(IReadOnlyList<StageObject> objects, Vec3 origin, Vec3 direction, double maxDistance = 200)
    {
        double? best = null;
        if (direction.Y < -1e-9)
        {
            var t = -origin.Y / direction.Y;
            if (t > 0 && t < maxDistance)
                best = t;
        }
        for (var i = 0; i < objects.Count; i++)
        {
            var obj = objects[i];
            if (obj.Kind == StageObjectKind.Truss)
                continue;
            if (RayBox(obj, origin, direction) is { } hit && hit > 1e-4 && (best is null || hit < best))
                best = hit;
        }
        return best;
    }

    /// <summary>
    /// Ray against an object's oriented box (slab test in the object's local frame): the entry distance,
    /// the exit distance when the origin is inside, or null on a miss.
    /// </summary>
    public static double? RayBox(StageObject obj, Vec3 origin, Vec3 direction)
    {
        var basis = BasisOf(obj);
        var o = basis.ToLocal(origin - obj.Position);
        var d = basis.ToLocal(direction);
        var half = obj.Size * 0.5;
        double near = double.NegativeInfinity, far = double.PositiveInfinity;
        if (!Slab(o.X, d.X, half.X, ref near, ref far) || !Slab(o.Y, d.Y, half.Y, ref near, ref far) || !Slab(o.Z, d.Z, half.Z, ref near, ref far))
            return null;
        if (near > far || far < 0)
            return null;
        return near >= 0 ? near : far;
    }

    /// <summary>Narrows [near, far] to one axis' slab; false when the ray runs parallel to it from outside.</summary>
    private static bool Slab(double origin, double direction, double half, ref double near, ref double far)
    {
        if (Math.Abs(direction) < 1e-12)
            return !(origin < -half || origin > half);
        var t1 = (-half - origin) / direction;
        var t2 = (half - origin) / direction;
        near = Math.Max(near, Math.Min(t1, t2));
        far = Math.Min(far, Math.Max(t1, t2));
        return true;
    }
}
