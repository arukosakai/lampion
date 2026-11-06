using System.Numerics;
using Lampion.Core.Meshes;
using Silk.NET.OpenGL;

namespace Lampion.App.Rendering.Models;

/// <summary>An imported model uploaded to the GPU: one mesh and optional colour texture per material. Render thread only.</summary>
internal sealed unsafe class GpuModel : IDisposable
{
    private readonly GL _gl;
    private readonly GpuModelPart[] _parts;

    /// <summary>Uploads <paramref name="model"/>; the GL context must be current.</summary>
    public GpuModel(GL gl, LoadedModel model)
    {
        _gl = gl;
        // Flat models have no depth; the same 1 cm the importer pads their box with keeps the scale finite.
        Size = Vector3.Max(model.Size, new Vector3(0.01f));
        _parts = new GpuModelPart[model.Parts.Count];
        for (var i = 0; i < _parts.Length; i++)
        {
            var (part, image) = model.Parts[i];
            _parts[i] = new GpuModelPart(new GpuMesh(gl, part.Vertices, part.Indices), part.Material, image is null ? 0 : Upload(image));
        }
    }

    /// <summary>Width, height and depth of the model as stored, metres; at least 1 cm on every axis.</summary>
    public Vector3 Size { get; }

    /// <summary>The model's parts, to draw with the lit program.</summary>
    public ReadOnlySpan<GpuModelPart> Parts => _parts;

    /// <summary>Deletes the meshes and textures.</summary>
    public void Dispose()
    {
        foreach (var part in _parts)
        {
            part.Mesh.Dispose();
            if (part.Texture != 0)
                _gl.DeleteTexture(part.Texture);
        }
    }

    private uint Upload(DecodedImage image)
    {
        var gl = _gl;
        var texture = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, texture);
        gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
        fixed (byte* p = image.Rgba)
            gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)image.Width, (uint)image.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
        gl.PixelStore(PixelStoreParameter.UnpackAlignment, 4);
        gl.GenerateMipmap(TextureTarget.Texture2D);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
        return texture;
    }
}
