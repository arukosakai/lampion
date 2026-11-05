namespace Lampion.Core.Model.Stage;

/// <summary>
/// An orthonormal frame: where an object's local X, Y and Z axes point in the world. Built from yaw/pitch/roll
/// in the same order as <c>System.Numerics.Quaternion.CreateFromYawPitchRoll</c>, so Core maths and the renderer agree.
/// </summary>
/// <remarks>72 bytes: pass it by <c>in</c>.</remarks>
internal readonly struct Basis
{
    private const double Deg = Math.PI / 180;

    private Basis(Vec3 x, Vec3 y, Vec3 z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>World direction of the local X axis.</summary>
    public Vec3 X { get; }

    /// <summary>World direction of the local Y axis.</summary>
    public Vec3 Y { get; }

    /// <summary>World direction of the local Z axis.</summary>
    public Vec3 Z { get; }

    /// <summary>Roll around Z first, then pitch around X, then yaw around Y; all in degrees, positive counter-clockwise looking down the axis.</summary>
    public static Basis FromYawPitchRoll(double yawDegrees, double pitchDegrees, double rollDegrees)
    {
        double sy = Math.Sin(yawDegrees * Deg), cy = Math.Cos(yawDegrees * Deg);
        double sp = Math.Sin(pitchDegrees * Deg), cp = Math.Cos(pitchDegrees * Deg);
        double sr = Math.Sin(rollDegrees * Deg), cr = Math.Cos(rollDegrees * Deg);
        return new Basis(Rotate(new Vec3(1, 0, 0)), Rotate(new Vec3(0, 1, 0)), Rotate(new Vec3(0, 0, 1)));

        Vec3 Rotate(Vec3 v)
        {
            v = new Vec3(v.X * cr - v.Y * sr, v.X * sr + v.Y * cr, v.Z);
            v = new Vec3(v.X, v.Y * cp - v.Z * sp, v.Y * sp + v.Z * cp);
            return new Vec3(v.X * cy + v.Z * sy, v.Y, -v.X * sy + v.Z * cy);
        }
    }

    /// <summary>A local vector expressed in world axes.</summary>
    public Vec3 ToWorld(Vec3 local) => X * local.X + Y * local.Y + Z * local.Z;

    /// <summary>A world vector expressed in local axes.</summary>
    public Vec3 ToLocal(Vec3 world) => new(Vec3.Dot(X, world), Vec3.Dot(Y, world), Vec3.Dot(Z, world));
}
