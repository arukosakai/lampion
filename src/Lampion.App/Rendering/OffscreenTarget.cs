using Silk.NET.OpenGL;

namespace Lampion.App.Rendering;

/// <summary>A framebuffer of renderbuffers, rebuilt only when its size, sample count or depth need change.</summary>
internal sealed class OffscreenTarget(GL gl) : IDisposable
{
    private uint _color;
    private uint _depth;
    private (int W, int H, int Samples, bool Depth) _shape;

    /// <summary>The GL framebuffer name; 0 until <see cref="Ensure"/> succeeds.</summary>
    public uint Framebuffer { get; private set; }

    /// <summary>Makes the target this shape, reallocating only if it changed. Leaves the framebuffer bound when it rebuilds.</summary>
    /// <param name="width">Pixels.</param>
    /// <param name="height">Pixels.</param>
    /// <param name="samples">MSAA samples; 0 for a plain target.</param>
    /// <param name="withDepth">Whether to attach a depth-stencil buffer.</param>
    /// <returns>False when the driver refuses this shape; the caller should then draw straight to the surface.</returns>
    public bool Ensure(int width, int height, int samples, bool withDepth)
    {
        if (Framebuffer != 0 && _shape == (width, height, samples, withDepth))
            return true;
        Dispose();
        Framebuffer = gl.GenFramebuffer();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, Framebuffer);
        _color = Storage(InternalFormat.Rgba8, FramebufferAttachment.ColorAttachment0, width, height, samples);
        if (withDepth)
            _depth = Storage(InternalFormat.Depth24Stencil8, FramebufferAttachment.DepthStencilAttachment, width, height, samples);
        _shape = (width, height, samples, withDepth);
        if (gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) == GLEnum.FramebufferComplete)
            return true;
        Dispose();
        return false;
    }

    private uint Storage(InternalFormat format, FramebufferAttachment attachment, int width, int height, int samples)
    {
        var buffer = gl.GenRenderbuffer();
        gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, buffer);
        gl.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, (uint)samples, format, (uint)width, (uint)height);
        gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, attachment, RenderbufferTarget.Renderbuffer, buffer);
        return buffer;
    }

    /// <summary>Deletes the framebuffer and its renderbuffers; <see cref="Ensure"/> can build it again.</summary>
    public void Dispose()
    {
        if (Framebuffer != 0)
            gl.DeleteFramebuffer(Framebuffer);
        if (_color != 0)
            gl.DeleteRenderbuffer(_color);
        if (_depth != 0)
            gl.DeleteRenderbuffer(_depth);
        Framebuffer = _color = _depth = 0;
    }
}
