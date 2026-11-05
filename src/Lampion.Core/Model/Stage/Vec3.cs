namespace Lampion.Core.Model.Stage;

/// <summary>A point or direction on the stage, in metres: X to the audience's right, Y up, Z toward the audience.</summary>
/// <param name="X">Metres to the audience's right.</param>
/// <param name="Y">Metres up from the floor.</param>
/// <param name="Z">Metres toward the audience.</param>
public readonly record struct Vec3(double X, double Y, double Z)
{
    /// <summary>Component-wise sum.</summary>
    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    /// <summary>Component-wise difference.</summary>
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    /// <summary>Scales every component by <paramref name="k"/>.</summary>
    public static Vec3 operator *(Vec3 a, double k) => new(a.X * k, a.Y * k, a.Z * k);

    /// <summary>Euclidean length in metres. Written to project files, so it stays a property.</summary>
    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

    /// <summary>This direction at unit length, or straight down (0, -1, 0) when it is (nearly) zero.</summary>
    public Vec3 Normalized()
    {
        var l = Length;
        return l < 1e-12 ? new Vec3(0, -1, 0) : this * (1 / l);
    }

    /// <summary>Dot product.</summary>
    public static double Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    /// <summary>Cross product (right-handed).</summary>
    public static Vec3 Cross(Vec3 a, Vec3 b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
}
