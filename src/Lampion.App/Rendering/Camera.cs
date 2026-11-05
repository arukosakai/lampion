using System.Numerics;

namespace Lampion.App.Rendering;

public enum ViewPreset
{
    Perspective,
    Top,
    Front,
    Side,
}

/// <summary>An orbit camera around <see cref="Target"/>; the axis presets switch to orthographic for exact placement.</summary>
public sealed class Camera
{
    public Vector3 Target { get; set; } = new(0, 2.2f, -1);
    public float Yaw { get; set; } = 0.35f;
    public float Pitch { get; set; } = 0.28f;
    public float Distance { get; set; } = 17;
    public float FieldOfView { get; set; } = 50 * MathF.PI / 180;
    public bool Orthographic { get; private set; }
    public ViewPreset Preset { get; private set; } = ViewPreset.Perspective;

    public Vector3 Position
    {
        get
        {
            var offset = new Vector3(MathF.Sin(Yaw) * MathF.Cos(Pitch), MathF.Sin(Pitch), MathF.Cos(Yaw) * MathF.Cos(Pitch));
            return Target + offset * Distance;
        }
    }

    public void Apply(ViewPreset preset)
    {
        Preset = preset;
        Orthographic = preset != ViewPreset.Perspective;
        (Yaw, Pitch) = preset switch
        {
            ViewPreset.Top => (0f, MathF.PI / 2 - 0.0001f),
            ViewPreset.Front => (0f, 0f),
            ViewPreset.Side => (MathF.PI / 2, 0f),
            _ => (0.35f, 0.28f),
        };
        if (preset == ViewPreset.Perspective)
            Distance = 17;
    }

    public void Orbit(float dx, float dy)
    {
        if (Orthographic)
        {
            // Rotating out of an axis view means the user wants to look around again.
            Preset = ViewPreset.Perspective;
            Orthographic = false;
        }
        Yaw -= dx * 0.008f;
        Pitch = Math.Clamp(Pitch + dy * 0.008f, -0.15f, MathF.PI / 2 - 0.01f);
    }

    public void Pan(float dx, float dy, float viewportHeight)
    {
        var (right, up, _) = Basis();
        var metresPerPixel = VisibleHeight() / Math.Max(1, viewportHeight);
        Target += (-right * dx + up * dy) * metresPerPixel;
    }

    public void Zoom(float steps) => Distance = Math.Clamp(Distance * MathF.Pow(0.88f, steps), 1.5f, 120);

    /// <summary>Height of the view at the target, in metres (also the ortho frustum height).</summary>
    public float VisibleHeight() => 2 * Distance * MathF.Tan(FieldOfView / 2);

    public (Vector3 Right, Vector3 Up, Vector3 Forward) Basis()
    {
        var forward = Vector3.Normalize(Target - Position);
        var worldUp = MathF.Abs(forward.Y) > 0.999f ? -Vector3.UnitZ : Vector3.UnitY;
        var right = Vector3.Normalize(Vector3.Cross(forward, worldUp));
        var up = Vector3.Cross(right, forward);
        return (right, up, forward);
    }

    public Matrix4x4 View()
    {
        var (_, up, _) = Basis();
        return Matrix4x4.CreateLookAt(Position, Target, up);
    }

    public Matrix4x4 Projection(float aspect)
    {
        if (Orthographic)
        {
            var h = VisibleHeight();
            return Matrix4x4.CreateOrthographic(h * aspect, h, 0.05f, 400);
        }
        return Matrix4x4.CreatePerspectiveFieldOfView(FieldOfView, aspect, 0.05f, 400);
    }

    /// <summary>World-space ray through a viewport point (pixels from the top-left).</summary>
    public (Vector3 Origin, Vector3 Direction) Ray(float x, float y, float width, float height)
    {
        var ndcX = 2 * x / width - 1;
        var ndcY = 1 - 2 * y / height;
        Matrix4x4.Invert(View() * Projection(width / height), out var inverse);
        var near = Vector4.Transform(new Vector4(ndcX, ndcY, -1, 1), inverse);
        var far = Vector4.Transform(new Vector4(ndcX, ndcY, 1, 1), inverse);
        var a = new Vector3(near.X, near.Y, near.Z) / near.W;
        var b = new Vector3(far.X, far.Y, far.Z) / far.W;
        return (a, Vector3.Normalize(b - a));
    }

    /// <summary>Viewport pixel of a world point, or null when it is behind the camera.</summary>
    public (float X, float Y)? Project(Vector3 world, float width, float height)
    {
        var clip = Vector4.Transform(new Vector4(world, 1), View() * Projection(width / height));
        if (clip.W <= 1e-4f)
            return null;
        var ndc = new Vector3(clip.X, clip.Y, clip.Z) / clip.W;
        return ((ndc.X + 1) / 2 * width, (1 - ndc.Y) / 2 * height);
    }
}
