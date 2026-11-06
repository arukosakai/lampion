using System.Numerics;

namespace Lampion.App.Rendering;

/// <summary>An orbit camera around <see cref="Target"/>; axis views switch to orthographic for exact placement, like Unity's scene gizmo.</summary>
internal sealed class Camera
{
    private const float NearlyVertical = MathF.PI / 2 - 0.0001f;
    private const float NearPlane = 0.05f;
    private const float FarPlane = 400;

    /// <summary>The point orbited around and looked at, in metres.</summary>
    public Vector3 Target { get; set; } = new(0, 2.2f, -1);

    /// <summary>Orbit angle around Y in radians; 0 looks from the audience (+Z).</summary>
    public float Yaw { get; set; } = 0.35f;

    /// <summary>Orbit elevation in radians; <see cref="Orbit"/> keeps it just short of ±π/2.</summary>
    public float Pitch { get; set; } = 0.28f;

    /// <summary>Metres from <see cref="Target"/>; <see cref="Zoom"/> keeps it within 1.5..120.</summary>
    public float Distance { get; set; } = 17;

    /// <summary>Vertical field of view in radians; also sizes the orthographic frustum.</summary>
    public float FieldOfView { get; set; } = 50 * MathF.PI / 180;

    /// <summary>Whether the projection is orthographic rather than perspective.</summary>
    public bool Orthographic { get; private set; }

    /// <summary>The axis view last applied, or <see cref="ViewPreset.Free"/> once orbited.</summary>
    public ViewPreset Preset { get; private set; } = ViewPreset.Free;

    /// <summary>What the scene gizmo labels the view: the axis name, or Persp / Iso when orbiting freely.</summary>
    public string Label =>
        Preset switch
        {
            ViewPreset.Top => "Top",
            ViewPreset.Bottom => "Bottom",
            ViewPreset.Front => "Front",
            ViewPreset.Back => "Back",
            ViewPreset.Right => "Right",
            ViewPreset.Left => "Left",
            _ => Orthographic ? "Iso" : "Persp",
        };

    /// <summary>Swaps perspective and orthographic, keeping the angle.</summary>
    public void ToggleProjection() => Orthographic = !Orthographic;

    /// <summary>The default three-quarter perspective view.</summary>
    public void Reset()
    {
        Preset = ViewPreset.Free;
        Orthographic = false;
        (Yaw, Pitch, Distance) = (0.35f, 0.28f, 17);
        Target = new Vector3(0, 2.2f, -1);
    }

    /// <summary>Where the eye is, in metres.</summary>
    public Vector3 Position
    {
        get
        {
            var offset = new Vector3(MathF.Sin(Yaw) * MathF.Cos(Pitch), MathF.Sin(Pitch), MathF.Cos(Yaw) * MathF.Cos(Pitch));
            return Target + offset * Distance;
        }
    }

    /// <summary>Looks straight down an axis, orthographic — what clicking a ball on the scene gizmo does.</summary>
    public void Apply(ViewPreset preset)
    {
        if (preset == ViewPreset.Free)
        {
            Reset();
            return;
        }
        Preset = preset;
        Orthographic = true;
        // The camera sits on the named side of the stage: Front is the audience at +Z, Right is +X.
        (Yaw, Pitch) = preset switch
        {
            ViewPreset.Top => (0f, NearlyVertical),
            ViewPreset.Bottom => (0f, -NearlyVertical),
            ViewPreset.Back => (MathF.PI, 0f),
            ViewPreset.Right => (MathF.PI / 2, 0f),
            ViewPreset.Left => (-MathF.PI / 2, 0f),
            _ => (0f, 0f),
        };
    }

    /// <summary>Turns the view by a pointer movement in pixels.</summary>
    public void Orbit(float dx, float dy)
    {
        // Orbiting leaves the axis view but keeps the projection, as Unity does (an ortho orbit is "Iso").
        Preset = ViewPreset.Free;
        Yaw -= dx * 0.008f;
        Pitch = Math.Clamp(Pitch + dy * 0.008f, -NearlyVertical, NearlyVertical);
    }

    /// <summary>Slides the target so the stage follows a pointer movement of (dx, dy) pixels.</summary>
    public void Pan(float dx, float dy, float viewportHeight)
    {
        var (right, up, _) = Basis();
        var metresPerPixel = VisibleHeight() / Math.Max(1, viewportHeight);
        Target += (-right * dx + up * dy) * metresPerPixel;
    }

    /// <summary>Moves in (positive) or out (negative) by wheel notches.</summary>
    public void Zoom(float steps) => Distance = Math.Clamp(Distance * MathF.Pow(0.88f, steps), 1.5f, 120);

    /// <summary>Height of the view at the target, in metres (also the ortho frustum height).</summary>
    public float VisibleHeight() => 2 * Distance * MathF.Tan(FieldOfView / 2);

    /// <summary>The camera's unit axes in world space: screen right, screen up and the view direction.</summary>
    public (Vector3 Right, Vector3 Up, Vector3 Forward) Basis() => Basis(Position);

    private (Vector3 Right, Vector3 Up, Vector3 Forward) Basis(Vector3 position)
    {
        var forward = Vector3.Normalize(Target - position);
        var worldUp = MathF.Abs(forward.Y) > 0.999f ? -Vector3.UnitZ : Vector3.UnitY;
        var right = Vector3.Normalize(Vector3.Cross(forward, worldUp));
        var up = Vector3.Cross(right, forward);
        return (right, up, forward);
    }

    /// <summary>World-to-view matrix (System.Numerics row-vector convention).</summary>
    public Matrix4x4 View()
    {
        var position = Position;
        var (_, up, _) = Basis(position);
        return Matrix4x4.CreateLookAt(position, Target, up);
    }

    /// <summary>View-to-clip matrix for a viewport with this width / height ratio.</summary>
    public Matrix4x4 Projection(float aspect)
    {
        if (Orthographic)
        {
            var h = VisibleHeight();
            return Matrix4x4.CreateOrthographic(h * aspect, h, NearPlane, FarPlane);
        }
        return Matrix4x4.CreatePerspectiveFieldOfView(FieldOfView, aspect, NearPlane, FarPlane);
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

    /// <summary>How many metres one screen pixel spans at <paramref name="world"/>, for a viewport <paramref name="viewportHeight"/> pixels tall.</summary>
    public float MetresPerPixel(Vector3 world, float viewportHeight)
    {
        if (Orthographic)
            return VisibleHeight() / viewportHeight;
        var position = Position;
        var depth = MathF.Max(NearPlane, Vector3.Dot(world - position, Basis(position).Forward));
        return 2 * depth * MathF.Tan(FieldOfView / 2) / viewportHeight;
    }

    /// <summary>The current view frozen for projecting many points into a viewport of this size, in pixels.</summary>
    public ScreenProjector Projector(float width, float height) => new(View() * Projection(width / height), width, height);

    /// <summary>Viewport pixel of a world point, or null when it is behind the camera.</summary>
    public (float X, float Y)? Project(Vector3 world, float width, float height) =>
        Projector(width, height).TryProject(world, out var screen) ? (screen.X, screen.Y) : null;
}
