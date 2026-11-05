using Silk.NET.OpenGL;

namespace Lampion.App.Rendering;

/// <summary>
/// Where a frame is drawn. Straight onto Avalonia's surface when it can be; otherwise offscreen
/// (multisampled, at reduced size, or both), then resolved and scaled onto the surface by <see cref="Present"/>.
/// </summary>
internal sealed class FrameTargets : IDisposable
{
    private readonly GL _gl;

    // A multisampled blit can't resize, so a scaled MSAA frame is resolved into a plain target first.
    private readonly OffscreenTarget _scene;
    private readonly OffscreenTarget _resolve;

    private uint _surface;
    private int _width;
    private int _height;
    private bool _offscreen;
    private bool _resolveFirst;

    /// <summary>Queries the GPU's MSAA limit; the GL context must be current.</summary>
    public FrameTargets(GL gl)
    {
        _gl = gl;
        gl.GetInteger(GLEnum.MaxSamples, out int maxSamples);
        MaxSamples = maxSamples;
        _scene = new OffscreenTarget(gl);
        _resolve = new OffscreenTarget(gl);
    }

    /// <summary>Most MSAA samples the GPU offers.</summary>
    public int MaxSamples { get; }

    /// <summary>Width of the picture being drawn this frame, in pixels (scaled by the render scale when offscreen).</summary>
    public int DrawWidth { get; private set; }

    /// <summary>Height of the picture being drawn this frame, in pixels.</summary>
    public int DrawHeight { get; private set; }

    /// <summary>Binds the framebuffer to draw this frame into and sets the viewport to <see cref="DrawWidth"/> × <see cref="DrawHeight"/>.</summary>
    /// <param name="surface">Avalonia's framebuffer for the control.</param>
    /// <param name="width">Surface width in pixels.</param>
    /// <param name="height">Surface height in pixels.</param>
    /// <param name="fidelity">Render scale and MSAA wanted; samples are capped at <see cref="MaxSamples"/>.</param>
    public void Begin(int surface, int width, int height, StageFidelity fidelity)
    {
        var samples = Math.Min(fidelity.Samples, MaxSamples);
        if (samples < 2)
            samples = 0;
        var scaledWidth = Math.Max(1, (int)Math.Round(width * fidelity.RenderScale));
        var scaledHeight = Math.Max(1, (int)Math.Round(height * fidelity.RenderScale));
        var scaled = scaledWidth != width || scaledHeight != height;
        _offscreen = (samples > 0 || scaled) && _scene.Ensure(scaledWidth, scaledHeight, samples, withDepth: true);
        if (!_offscreen)
            (scaledWidth, scaledHeight) = (width, height);
        _resolveFirst = samples > 0 && scaled;
        (_surface, _width, _height) = ((uint)surface, width, height);
        (DrawWidth, DrawHeight) = (scaledWidth, scaledHeight);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _offscreen ? _scene.Framebuffer : _surface);
        _gl.Viewport(0, 0, (uint)scaledWidth, (uint)scaledHeight);
    }

    /// <summary>Copies an offscreen picture onto the surface, resolving MSAA and scaling up as needed; nothing when drawing went straight to the surface.</summary>
    public void Present()
    {
        if (!_offscreen)
            return;
        var source = _scene.Framebuffer;
        if (_resolveFirst && _resolve.Ensure(DrawWidth, DrawHeight, samples: 0, withDepth: false))
        {
            Blit(source, _resolve.Framebuffer, DrawWidth, DrawHeight, BlitFramebufferFilter.Nearest);
            source = _resolve.Framebuffer;
        }
        var filter = DrawWidth == _width && DrawHeight == _height ? BlitFramebufferFilter.Nearest : BlitFramebufferFilter.Linear;
        Blit(source, _surface, _width, _height, filter);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _surface);
    }

    private void Blit(uint from, uint to, int toWidth, int toHeight, BlitFramebufferFilter filter)
    {
        _gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, from);
        _gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, to);
        _gl.BlitFramebuffer(0, 0, DrawWidth, DrawHeight, 0, 0, toWidth, toHeight, ClearBufferMask.ColorBufferBit, filter);
    }

    /// <summary>Frees the offscreen targets.</summary>
    public void Dispose()
    {
        _scene.Dispose();
        _resolve.Dispose();
    }
}
