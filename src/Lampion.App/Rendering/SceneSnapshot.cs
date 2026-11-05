using System.Numerics;
using Lampion.Core.Model;

namespace Lampion.App.Rendering;

/// <summary>What one fixture looks like this frame, already decoded from its DMX output.</summary>
public sealed record FixtureVisual(
    string Id,
    BeamKind Kind,
    Vector3 Position,
    Vector3 BeamDirection,
    float BeamAngle,
    Vector3 Light,
    float BeamLength,
    bool MovingHead,
    bool Selected
)
{
    /// <summary>Front of the lens, where the beam starts.</summary>
    public Vector3 Lens => Position + BeamDirection * LensOffset;

    public float LensOffset =>
        Kind switch
        {
            BeamKind.Bar => 0.06f,
            BeamKind.Strobe or BeamKind.Blinder => 0.08f,
            BeamKind.Spot => 0.18f,
            _ => 0.16f,
        };

    public float Brightness => MathF.Max(Light.X, MathF.Max(Light.Y, Light.Z));
}

public sealed record ObjectVisual(string Id, StageObjectKind Kind, Vector3 Position, Vector3 Size, float Yaw, Vector3 Color, bool Selected);

/// <summary>Latest decoded video picture, BGRA, shared between the media player thread and the renderer.</summary>
public sealed class VideoFrame
{
    public byte[] Pixels = [];
    public int Width;
    public int Height;
    public int Version;
}

/// <summary>An immutable picture of the stage handed from the UI thread to the GL renderer.</summary>
public sealed record SceneSnapshot(
    IReadOnlyList<FixtureVisual> Fixtures,
    IReadOnlyList<ObjectVisual> Objects,
    float Haze,
    float Ambient,
    float GridStrength,
    Vector3 Bounce,
    float Time,
    VideoFrame? Video,
    Vector3? AimTarget
);
