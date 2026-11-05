using System.Numerics;
using Avalonia;
using Lampion.Core.Model.Stage;

namespace Lampion.App.Controls.Stage3D;

/// <summary>Small conversions and 2D/3D tests shared by the stage view's collaborators. Allocation-free.</summary>
internal static class StageGeometry
{
    /// <summary>A model point (double metres) as a render vector.</summary>
    public static Vector3 ToVector(Vec3 v) => new((float)v.X, (float)v.Y, (float)v.Z);

    /// <summary>A render vector as a model point.</summary>
    public static Vec3 ToVec(Vector3 v) => new(v.X, v.Y, v.Z);

    /// <summary>A <c>#RRGGBB</c> colour as linear RGB (gamma 2.2 undone); unparseable text gives white.</summary>
    public static Vector3 LinearColor(string hex)
    {
        var c = Core.Color.Rgb.FromHex(hex);
        return new Vector3(MathF.Pow((float)c.R, 2.2f), MathF.Pow((float)c.G, 2.2f), MathF.Pow((float)c.B, 2.2f));
    }

    /// <summary>Where a ray meets the horizontal plane at height <paramref name="y"/>, or null when it runs parallel or points away.</summary>
    public static Vector3? IntersectPlaneY(Vector3 origin, Vector3 direction, float y)
    {
        if (MathF.Abs(direction.Y) < 1e-5f)
            return null;
        var t = (y - origin.Y) / direction.Y;
        return t > 0 ? origin + direction * t : null;
    }

    /// <summary>Pixels from <paramref name="p"/> to the segment a–b.</summary>
    public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var t = Math.Clamp(Vector2.Dot(p - a, ab) / Math.Max(1e-5f, ab.LengthSquared()), 0, 1);
        return Vector2.Distance(p, a + ab * t);
    }

    /// <summary>Pixels between two pointer positions.</summary>
    public static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
