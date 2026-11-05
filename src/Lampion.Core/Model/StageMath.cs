namespace Lampion.Core.Model;

/// <summary>Geometry for the stage builder. Everything is in metres and degrees.</summary>
public static class StageMath
{
    private const double Deg = Math.PI / 180;

    /// <summary>Unit vector for a yaw/pitch aim (yaw 0 faces the audience, pitch -90 points down).</summary>
    public static Vec3 Direction(double yawDegrees, double pitchDegrees)
    {
        var yaw = yawDegrees * Deg;
        var pitch = pitchDegrees * Deg;
        return new Vec3(Math.Sin(yaw) * Math.Cos(pitch), Math.Sin(pitch), Math.Cos(yaw) * Math.Cos(pitch));
    }

    /// <summary>Where the beam points once a moving head's pan/tilt (0..1, centre 0.5) is applied to its rigging aim.</summary>
    public static Vec3 BeamDirection(Fixture fixture, FixtureProfile? profile, double pan, double tilt)
    {
        if (profile is null || !profile.HasPanTilt)
            return Direction(fixture.Yaw, fixture.Pitch);
        return Direction(fixture.Yaw + (pan - 0.5) * profile.PanRange, fixture.Pitch + (tilt - 0.5) * profile.TiltRange);
    }

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
        var (yaw, pitch) = AimAt(fixture.Position, target);
        (double Pan, double Tilt)? best = null;
        var bestDistance = double.MaxValue;
        // The same direction as (yaw, pitch), seen from a head flipped over the top.
        foreach (var (y, p) in new[] { (yaw, pitch), (yaw + 180, -180 - pitch) })
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
        return best;
    }

    public static void AimAt(Fixture fixture, Vec3 target)
    {
        var (yaw, pitch) = AimAt(fixture.Position, target);
        fixture.Yaw = Math.Round(yaw, 2);
        fixture.Pitch = Math.Round(pitch, 2);
    }

    /// <summary>A point in an object's local frame (metres from its centre) moved into the world.</summary>
    public static Vec3 LocalToWorld(StageObject obj, Vec3 local)
    {
        var yaw = obj.Yaw * Deg;
        var cos = Math.Cos(yaw);
        var sin = Math.Sin(yaw);
        return obj.Position + new Vec3(local.X * cos + local.Z * sin, local.Y, -local.X * sin + local.Z * cos);
    }

    /// <summary>
    /// Spreads fixtures evenly along a truss, hung <paramref name="drop"/> metres below its bottom chord,
    /// with half a gap at each end so they sit centred like on a real bar.
    /// </summary>
    public static void DistributeAlong(StageObject truss, IReadOnlyList<Fixture> fixtures, double drop = 0.35)
    {
        if (fixtures.Count == 0)
            return;
        var length = truss.Size.X;
        var gap = length / fixtures.Count;
        for (var i = 0; i < fixtures.Count; i++)
        {
            var along = -length / 2 + gap * (i + 0.5);
            var p = LocalToWorld(truss, new Vec3(along, -truss.Size.Y / 2 - drop, 0));
            fixtures[i].Position = Round(p);
        }
    }

    public static double Snap(double value, double step) => step <= 0 ? value : Math.Round(value / step) * step;

    public static Vec3 Snap(Vec3 v, double step) => new(Snap(v.X, step), Snap(v.Y, step), Snap(v.Z, step));

    public static Vec3 Round(Vec3 v, int digits = 3) => new(Math.Round(v.X, digits), Math.Round(v.Y, digits), Math.Round(v.Z, digits));

    /// <summary>Distance along the ray to the first stage surface hit (objects and the floor at y = 0), or null.</summary>
    public static double? Raycast(IEnumerable<StageObject> objects, Vec3 origin, Vec3 direction, double maxDistance = 200)
    {
        double? best = null;
        if (direction.Y < -1e-9)
        {
            var t = -origin.Y / direction.Y;
            if (t > 0 && t < maxDistance)
                best = t;
        }
        foreach (var obj in objects)
        {
            if (obj.Kind == StageObjectKind.Truss)
                continue;
            if (RayBox(obj, origin, direction) is { } hit && hit > 1e-4 && (best is null || hit < best))
                best = hit;
        }
        return best;
    }

    /// <summary>Ray against an object's oriented box (slab test in the object's local frame).</summary>
    public static double? RayBox(StageObject obj, Vec3 origin, Vec3 direction)
    {
        var yaw = -obj.Yaw * Deg;
        Vec3 ToLocal(Vec3 v) => new(v.X * Math.Cos(yaw) + v.Z * Math.Sin(yaw), v.Y, -v.X * Math.Sin(yaw) + v.Z * Math.Cos(yaw));
        var o = ToLocal(origin - obj.Position);
        var d = ToLocal(direction);
        var half = obj.Size * 0.5;
        double near = double.NegativeInfinity, far = double.PositiveInfinity;
        foreach (var (oc, dc, h) in new[] { (o.X, d.X, half.X), (o.Y, d.Y, half.Y), (o.Z, d.Z, half.Z) })
        {
            if (Math.Abs(dc) < 1e-12)
            {
                if (oc < -h || oc > h)
                    return null;
                continue;
            }
            var t1 = (-h - oc) / dc;
            var t2 = (h - oc) / dc;
            near = Math.Max(near, Math.Min(t1, t2));
            far = Math.Min(far, Math.Max(t1, t2));
        }
        if (near > far || far < 0)
            return null;
        return near >= 0 ? near : far;
    }
}
