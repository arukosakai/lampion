using System.Numerics;

namespace Lampion.App.Rendering;

/// <summary>
/// Readies the lit program for a frame: camera, room light, the LED-screen video, and the brightest
/// fixtures as spot lights. The floor, set-piece and fixture-body passes then draw with it.
/// </summary>
internal sealed class SurfaceLighting
{
    // Dimmer than this, a fixture adds nothing visible and isn't worth one of the limited light slots.
    private const float MinBrightness = 0.004f;
    private const float Exposure = 1.1f;
    private const int VideoUnit = 0;

    /// <summary>Texture unit imported models bind their colour texture to.</summary>
    public const int AlbedoMapUnit = 1;

    private readonly LitProgram _lit;
    private readonly VideoTexture _video;
    private readonly int[] _chosen = new int[Shaders.MaxLights];

    /// <summary>Lights surfaces through <paramref name="lit"/>, showing <paramref name="video"/> on screens.</summary>
    public SurfaceLighting(LitProgram lit, VideoTexture video)
    {
        _lit = lit;
        _video = video;
    }

    /// <summary>Makes the lit program current and uploads everything that is the same for every surface this frame.</summary>
    /// <param name="scene">The frame's scene.</param>
    /// <param name="fixtures">Per-fixture data parallel to <see cref="SceneSnapshot.Fixtures"/>.</param>
    /// <param name="view">World-to-view matrix.</param>
    /// <param name="projection">View-to-clip matrix.</param>
    /// <param name="eye">Camera position, metres.</param>
    /// <param name="maxLights">How many fixtures may light surfaces; clamped to 1..<see cref="Shaders.MaxLights"/>.</param>
    public void Begin(SceneSnapshot scene, ReadOnlySpan<FixtureDrawData> fixtures, in Matrix4x4 view, in Matrix4x4 projection, Vector3 eye, int maxLights)
    {
        _lit.Use();
        _lit.SetCamera(in view, in projection, eye);
        _lit.SetRoom(scene.Ambient, scene.Bounce, scene.Haze, Exposure);
        _lit.SetVideo(VideoUnit, _video.HasVideo);
        _video.Bind();
        _lit.SetAlbedoMapUnit(AlbedoMapUnit);
        _lit.SetAlbedoMap(false);
        UploadLights(scene.Fixtures, fixtures, Math.Clamp(maxLights, 1, Shaders.MaxLights));
    }

    private void UploadLights(ReadOnlySpan<FixtureVisual> visuals, ReadOnlySpan<FixtureDrawData> data, int limit)
    {
        var count = ChooseBrightest(visuals, limit);
        Span<Vector3> pos = stackalloc Vector3[Shaders.MaxLights];
        Span<Vector3> dir = stackalloc Vector3[Shaders.MaxLights];
        Span<Vector3> col = stackalloc Vector3[Shaders.MaxLights];
        Span<Vector2> cone = stackalloc Vector2[Shaders.MaxLights];
        for (var i = 0; i < count; i++)
        {
            ref readonly var f = ref visuals[_chosen[i]];
            ref readonly var d = ref data[_chosen[i]];
            var half = f.BeamAngle * 0.5f * MathF.PI / 180;
            pos[i] = d.Lens;
            dir[i] = f.BeamDirection;
            col[i] = f.Light * d.Power;
            cone[i] = new Vector2(MathF.Cos(MathF.Min(1.5f, half * 1.15f)), MathF.Cos(half * 0.55f));
        }
        _lit.SetLights(count, pos, dir, col, cone);
    }

    /// <summary>
    /// Fills <see cref="_chosen"/> with the indices of the brightest lit fixtures, brightest first and ties
    /// in scene order (a stable sort, so the same lights win frame after frame).
    /// </summary>
    private int ChooseBrightest(ReadOnlySpan<FixtureVisual> visuals, int limit)
    {
        var count = 0;
        for (var i = 0; i < visuals.Length; i++)
        {
            var brightness = visuals[i].Brightness;
            if (brightness <= MinBrightness)
                continue;
            var at = count;
            while (at > 0 && visuals[_chosen[at - 1]].Brightness < brightness)
                at--;
            if (at >= limit)
                continue;
            var last = Math.Min(count, limit - 1);
            for (var j = last; j > at; j--)
                _chosen[j] = _chosen[j - 1];
            _chosen[at] = i;
            if (count < limit)
                count++;
        }
        return count;
    }
}
