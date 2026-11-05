using System.Runtime.InteropServices;
using Silk.NET.OpenGL;

namespace Lampion.App.Rendering;

/// <summary>A <see cref="MeshBuilder"/> uploaded into a vertex array with its own vertex and index buffers.</summary>
internal sealed unsafe class GpuMesh : IDisposable
{
    private readonly GL _gl;
    private readonly uint _vao;
    private readonly uint _vbo;
    private readonly uint _ebo;
    private readonly uint _count;

    /// <summary>Uploads <paramref name="source"/> as static geometry; the GL context must be current.</summary>
    public GpuMesh(GL gl, MeshBuilder source)
    {
        _gl = gl;
        _vao = gl.GenVertexArray();
        gl.BindVertexArray(_vao);
        _vbo = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        gl.BufferData<float>(BufferTargetARB.ArrayBuffer, CollectionsMarshal.AsSpan(source.Vertices), BufferUsageARB.StaticDraw);
        _ebo = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        gl.BufferData<uint>(BufferTargetARB.ElementArrayBuffer, CollectionsMarshal.AsSpan(source.Indices), BufferUsageARB.StaticDraw);
        const uint stride = MeshBuilder.FloatsPerVertex * sizeof(float);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
        gl.EnableVertexAttribArray(2);
        gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, (void*)(6 * sizeof(float)));
        gl.BindVertexArray(0);
        _count = (uint)source.Indices.Count;
    }

    /// <summary>Draws the triangles with whatever program and uniforms are current.</summary>
    public void Draw()
    {
        _gl.BindVertexArray(_vao);
        _gl.DrawElements(PrimitiveType.Triangles, _count, DrawElementsType.UnsignedInt, (void*)0);
    }

    /// <summary>Deletes the vertex array and its buffers.</summary>
    public void Dispose()
    {
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
    }
}
