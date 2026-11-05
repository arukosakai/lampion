using Silk.NET.OpenGL;

namespace Lampion.App.Rendering;

/// <summary>The LED-screen texture, re-uploaded from a <see cref="VideoFrame"/> only when its picture changes.</summary>
internal sealed unsafe class VideoTexture(GL gl) : IDisposable
{
    private uint _texture;
    private int _version = -1;

    /// <summary>Whether the last <see cref="Update"/> had a picture to show.</summary>
    public bool HasVideo { get; private set; }

    /// <summary>Uploads the frame's picture if it is new; locks the frame while reading it.</summary>
    /// <param name="frame">The shared video frame, or null for no video.</param>
    public void Update(VideoFrame? frame)
    {
        if (frame is null || frame.Width == 0)
        {
            HasVideo = false;
            return;
        }
        if (_texture == 0)
        {
            _texture = gl.GenTexture();
            gl.BindTexture(TextureTarget.Texture2D, _texture);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        }
        HasVideo = true;
        if (frame.Version == _version)
            return;
        lock (frame)
        {
            _version = frame.Version;
            gl.BindTexture(TextureTarget.Texture2D, _texture);
            // Uploaded as RGBA and swizzled to .bgr in the shader: GLES has no portable BGRA upload.
            fixed (byte* p = frame.Pixels)
                gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)frame.Width, (uint)frame.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
        }
    }

    /// <summary>Binds the texture (0, i.e. none, before the first picture) to texture unit 0.</summary>
    public void Bind()
    {
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, _texture);
    }

    /// <summary>Deletes the texture.</summary>
    public void Dispose()
    {
        if (_texture != 0)
            gl.DeleteTexture(_texture);
        _texture = 0;
    }
}
