using System.Numerics;
using Silk.NET.OpenGL;

namespace Lampion.App.Rendering;

/// <summary>
/// A linked GL program with the stage's vertex layout bound (0 position, 1 normal, 2 uv). Uniforms are
/// set by location, resolved once with <see cref="Location"/>, so per-frame uploads do no string work.
/// </summary>
internal sealed unsafe class ShaderProgram : IDisposable
{
    private readonly GL _gl;

    /// <summary>Compiles and links the program; the GL context must be current.</summary>
    /// <param name="gl">The context's API.</param>
    /// <param name="header">#version line(s) from <see cref="Shaders.Header"/>, prepended to both stages.</param>
    /// <param name="vertex">Vertex stage source.</param>
    /// <param name="fragment">Fragment stage source.</param>
    /// <exception cref="InvalidOperationException">A stage fails to compile or the program fails to link; the message has the driver's log.</exception>
    public ShaderProgram(GL gl, string header, string vertex, string fragment)
    {
        _gl = gl;
        var vs = Compile(ShaderType.VertexShader, header + vertex);
        var fs = Compile(ShaderType.FragmentShader, header + fragment);
        Handle = gl.CreateProgram();
        gl.AttachShader(Handle, vs);
        gl.AttachShader(Handle, fs);
        gl.BindAttribLocation(Handle, 0, "aPos");
        gl.BindAttribLocation(Handle, 1, "aNormal");
        gl.BindAttribLocation(Handle, 2, "aUv");
        gl.LinkProgram(Handle);
        gl.GetProgram(Handle, ProgramPropertyARB.LinkStatus, out var linked);
        gl.DeleteShader(vs);
        gl.DeleteShader(fs);
        if (linked == 0)
            throw new InvalidOperationException("Shader link failed: " + gl.GetProgramInfoLog(Handle));
    }

    /// <summary>The GL program name.</summary>
    public uint Handle { get; }

    private uint Compile(ShaderType type, string source)
    {
        var shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);
        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out var ok);
        if (ok == 0)
            throw new InvalidOperationException($"{type} compile failed: {_gl.GetShaderInfoLog(shader)}");
        return shader;
    }

    /// <summary>Makes this the current program.</summary>
    public void Use() => _gl.UseProgram(Handle);

    /// <summary>A uniform's location (for arrays, name the first element: <c>uLights[0]</c>); -1 when the driver optimised it away, which GL ignores on upload.</summary>
    public int Location(string name) => _gl.GetUniformLocation(Handle, name);

    /// <summary>Uploads a float; the program must be current.</summary>
    public void Set(int location, float v) => _gl.Uniform1(location, v);

    /// <summary>Uploads an int or sampler unit; the program must be current.</summary>
    public void Set(int location, int v) => _gl.Uniform1(location, v);

    /// <summary>Uploads a vec3; the program must be current.</summary>
    public void Set(int location, Vector3 v) => _gl.Uniform3(location, v.X, v.Y, v.Z);

    // System.Numerics is row-major with row vectors; GL reads the same memory column-major, which is
    // exactly the transpose GLSL's column-vector convention needs — so no transpose flag.
    /// <summary>Uploads a mat4; the program must be current.</summary>
    public void Set(int location, in Matrix4x4 m)
    {
        fixed (Matrix4x4* p = &m)
            _gl.UniformMatrix4(location, 1, false, (float*)p);
    }

    /// <summary>Uploads a vec3 array starting at element 0; the program must be current.</summary>
    public void Set(int location, ReadOnlySpan<Vector3> values)
    {
        fixed (Vector3* p = values)
            _gl.Uniform3(location, (uint)values.Length, (float*)p);
    }

    /// <summary>Uploads a vec2 array starting at element 0; the program must be current.</summary>
    public void Set(int location, ReadOnlySpan<Vector2> values)
    {
        fixed (Vector2* p = values)
            _gl.Uniform2(location, (uint)values.Length, (float*)p);
    }

    /// <summary>Deletes the program.</summary>
    public void Dispose() => _gl.DeleteProgram(Handle);
}
