using System.Numerics;
using Silk.NET.OpenGL;

namespace Lampion.App.Rendering;

/// <summary>
/// Draws the stage: lit floor, set and fixtures, then additive volumetric beams and lens glows. Depending on
/// the <see cref="StageFidelity"/> it renders offscreen (multisampled and/or at reduced size) and then resolves
/// and scales the picture onto the surface Avalonia hands us. Owns its GL resources; create, use and dispose
/// it with the same GL context current.
/// </summary>
internal sealed class StageRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly LitProgram _lit;
    private readonly BeamProgram _beam;
    private readonly GlowProgram _glow;
    private readonly StageMeshes _meshes;
    private readonly VideoTexture _video;
    private readonly FrameTargets _targets;
    private readonly SurfaceLighting _lighting;
    private readonly FloorPass _floor;
    private readonly SetPiecePass _setPieces;
    private readonly FixtureBodyPass _bodies;
    private readonly BeamPass _beams;
    private readonly GlowPass _glows;
    private FixtureDrawData[] _fixtureData = [];

    /// <summary>Compiles the shaders and uploads the base meshes.</summary>
    /// <param name="gl">The current context's API.</param>
    /// <param name="isEmbedded">True on OpenGL ES (ANGLE), which needs GLSL ES headers.</param>
    /// <exception cref="InvalidOperationException">A shader fails to compile or link.</exception>
    public StageRenderer(GL gl, bool isEmbedded)
    {
        _gl = gl;
        var header = Shaders.Header(isEmbedded);
        _lit = new LitProgram(gl, header);
        _beam = new BeamProgram(gl, header);
        _glow = new GlowProgram(gl, header);
        _meshes = new StageMeshes(gl);
        _video = new VideoTexture(gl);
        _targets = new FrameTargets(gl);
        _lighting = new SurfaceLighting(_lit, _video);
        _floor = new FloorPass(_lit, _meshes);
        _setPieces = new SetPiecePass(_lit, _meshes);
        _bodies = new FixtureBodyPass(_lit, _meshes);
        _beams = new BeamPass(_beam, _meshes);
        _glows = new GlowPass(_glow, _meshes);
        Info = $"{gl.GetStringS(StringName.Renderer)} · {gl.GetStringS(StringName.Version)} · up to MSAA {_targets.MaxSamples}x";
    }

    /// <summary>GPU, driver version and MSAA limit, for the corner of the view.</summary>
    public string Info { get; }

    /// <summary>Draws one frame of <paramref name="scene"/> into Avalonia's framebuffer. Allocation-free once warmed up.</summary>
    /// <param name="scene">What to draw.</param>
    /// <param name="camera">Where from.</param>
    /// <param name="targetFramebuffer">Avalonia's framebuffer for the control.</param>
    /// <param name="width">Surface width in pixels.</param>
    /// <param name="height">Surface height in pixels.</param>
    /// <param name="fidelity">How much GPU work to spend.</param>
    public void Render(SceneSnapshot scene, Camera camera, int targetFramebuffer, int width, int height, StageFidelity fidelity)
    {
        var gl = _gl;
        _targets.Begin(targetFramebuffer, width, height, fidelity);
        gl.ClearColor(0.008f, 0.009f, 0.012f, 1);
        gl.DepthMask(true);
        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        gl.Enable(EnableCap.DepthTest);
        gl.DepthFunc(DepthFunction.Lequal);
        gl.Disable(EnableCap.Blend);
        gl.Disable(EnableCap.CullFace);

        var view = camera.View();
        var projection = camera.Projection(width / (float)Math.Max(1, height));
        var eye = camera.Position;
        var fixtures = scene.Fixtures;
        var data = PrepareFixtures(fixtures);
        _video.Update(scene.Video);

        _lighting.Begin(scene, data, in view, in projection, eye, fidelity.MaxLights);
        _floor.Draw(scene.GridStrength);
        _setPieces.Draw(scene.Objects);
        _bodies.Draw(fixtures, data);

        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(BlendingFactor.One, BlendingFactor.One);
        gl.DepthMask(false);
        _beams.Draw(scene, data, in view, in projection, eye, fidelity.HazeSteps);
        _glows.Draw(fixtures, data, in view, in projection, eye);

        gl.DepthMask(true);
        gl.Disable(EnableCap.Blend);
        gl.Disable(EnableCap.DepthTest);
        gl.BindVertexArray(0);
        gl.UseProgram(0);

        _targets.Present();
        _meshes.EndFrame();
    }

    private ReadOnlySpan<FixtureDrawData> PrepareFixtures(ReadOnlySpan<FixtureVisual> fixtures)
    {
        if (_fixtureData.Length < fixtures.Length)
            _fixtureData = new FixtureDrawData[Math.Max(fixtures.Length, _fixtureData.Length * 2)];
        for (var i = 0; i < fixtures.Length; i++)
            _fixtureData[i] = new FixtureDrawData(in fixtures[i]);
        return _fixtureData.AsSpan(0, fixtures.Length);
    }

    /// <summary>Rotation taking +Z onto <paramref name="forward"/>; see <see cref="StageTransforms.Facing"/>.</summary>
    public static Matrix4x4 Facing(Vector3 forward) => StageTransforms.Facing(forward);

    /// <summary>Frees every GL resource the renderer made.</summary>
    public void Dispose()
    {
        _targets.Dispose();
        _video.Dispose();
        _meshes.Dispose();
        _lit.Dispose();
        _beam.Dispose();
        _glow.Dispose();
    }
}
