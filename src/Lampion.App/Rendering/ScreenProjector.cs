using System.Numerics;

namespace Lampion.App.Rendering;

/// <summary>
/// A camera's view-projection frozen for one viewport, so projecting many points (labels, ruler ticks)
/// costs one matrix transform each instead of rebuilding the camera matrices per point.
/// </summary>
internal readonly struct ScreenProjector
{
    private readonly Matrix4x4 _viewProjection;
    private readonly float _width;
    private readonly float _height;

    /// <summary>Wraps a view-projection matrix for a viewport of the given size in pixels.</summary>
    public ScreenProjector(Matrix4x4 viewProjection, float width, float height)
    {
        _viewProjection = viewProjection;
        _width = width;
        _height = height;
    }

    /// <summary>Viewport pixel (from the top-left) of a world point.</summary>
    /// <returns>False when the point is behind the camera; <paramref name="screen"/> is then zero.</returns>
    public bool TryProject(Vector3 world, out Vector2 screen)
    {
        var clip = Vector4.Transform(new Vector4(world, 1), _viewProjection);
        if (clip.W <= 1e-4f)
        {
            screen = default;
            return false;
        }
        var ndc = new Vector3(clip.X, clip.Y, clip.Z) / clip.W;
        screen = new Vector2((ndc.X + 1) / 2 * _width, (1 - ndc.Y) / 2 * _height);
        return true;
    }
}
