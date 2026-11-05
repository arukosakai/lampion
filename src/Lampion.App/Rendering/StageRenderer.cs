using System.Numerics;
using Lampion.Core.Model;
using Silk.NET.OpenGL;

namespace Lampion.App.Rendering;

internal sealed unsafe class GpuMesh : IDisposable
{
    private readonly GL gl;
    private readonly uint _vao;
    private readonly uint _vbo;
    private readonly uint _ebo;
    private readonly int _count;

    public GpuMesh(GL gl, MeshBuilder source)
    {
        this.gl = gl;
        _vao = Upload(gl, source, out _vbo, out _ebo, out _count);
    }

    private static uint Upload(GL gl, MeshBuilder source, out uint vbo, out uint ebo, out int count)
    {
        var vao = gl.GenVertexArray();
        gl.BindVertexArray(vao);
        vbo = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        var vertices = source.Vertices.ToArray();
        gl.BufferData<float>(BufferTargetARB.ArrayBuffer, vertices, BufferUsageARB.StaticDraw);
        ebo = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);
        var indices = source.Indices.ToArray();
        gl.BufferData<uint>(BufferTargetARB.ElementArrayBuffer, indices, BufferUsageARB.StaticDraw);
        const uint stride = 8 * sizeof(float);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
        gl.EnableVertexAttribArray(2);
        gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, (void*)(6 * sizeof(float)));
        gl.BindVertexArray(0);
        count = indices.Length;
        return vao;
    }

    public void Draw()
    {
        gl.BindVertexArray(_vao);
        gl.DrawElements(PrimitiveType.Triangles, (uint)_count, DrawElementsType.UnsignedInt, (void*)0);
    }

    public void Dispose()
    {
        gl.DeleteVertexArray(_vao);
        gl.DeleteBuffer(_vbo);
        gl.DeleteBuffer(_ebo);
    }
}

internal sealed unsafe class ShaderProgram : IDisposable
{
    private readonly GL _gl;
    private readonly Dictionary<string, int> _locations = [];

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

    public void Use() => _gl.UseProgram(Handle);

    private int Loc(string name)
    {
        if (!_locations.TryGetValue(name, out var loc))
            _locations[name] = loc = _gl.GetUniformLocation(Handle, name);
        return loc;
    }

    public void Set(string name, float v) => _gl.Uniform1(Loc(name), v);

    public void Set(string name, int v) => _gl.Uniform1(Loc(name), v);

    public void Set(string name, Vector3 v) => _gl.Uniform3(Loc(name), v.X, v.Y, v.Z);

    // System.Numerics is row-major with row vectors; GL reads the same memory column-major, which is
    // exactly the transpose GLSL's column-vector convention needs — so no transpose flag.
    public void Set(string name, Matrix4x4 m) => _gl.UniformMatrix4(Loc(name), 1, false, (float*)&m);

    public void SetArray(string name, ReadOnlySpan<Vector3> values)
    {
        fixed (Vector3* p = values)
            _gl.Uniform3(Loc(name + "[0]"), (uint)values.Length, (float*)p);
    }

    public void SetArray(string name, ReadOnlySpan<Vector2> values)
    {
        fixed (Vector2* p = values)
            _gl.Uniform2(Loc(name + "[0]"), (uint)values.Length, (float*)p);
    }

    public void Dispose() => _gl.DeleteProgram(Handle);
}

/// <summary>A framebuffer of renderbuffers, rebuilt only when its size, sample count or depth need change.</summary>
internal sealed class OffscreenTarget(GL gl) : IDisposable
{
    private uint _color;
    private uint _depth;
    private (int W, int H, int Samples, bool Depth) _shape;

    public uint Framebuffer { get; private set; }

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

/// <summary>
/// Draws the stage: lit set and fixtures, then additive volumetric beams and lens glows. Depending on the
/// <see cref="StageFidelity"/> it renders offscreen (multisampled and/or at reduced size) and then resolves
/// and scales the picture onto the surface Avalonia hands us.
/// </summary>
internal sealed unsafe class StageRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly ShaderProgram _lit;
    private readonly ShaderProgram _beam;
    private readonly ShaderProgram _glow;
    private readonly GpuMesh _box;
    private readonly GpuMesh _quad;
    private readonly GpuMesh _cylinder;
    private readonly GpuMesh _cone;
    private readonly Dictionary<(int, int, int), GpuMesh> _trusses = [];
    private uint _videoTexture;
    private int _videoVersion = -1;
    private bool _hasVideo;

    // Where the scene is drawn when it can't go straight to Avalonia's surface: multisampled, scaled, or both;
    // plus a plain target to resolve MSAA into before a scaled blit, since a multisampled blit can't resize.
    private readonly OffscreenTarget _scene;
    private readonly OffscreenTarget _resolve;
    private readonly int _maxSamples;

    public StageRenderer(GL gl, bool isEmbedded)
    {
        _gl = gl;
        var header = isEmbedded ? "#version 300 es\nprecision highp float;\nprecision highp int;\n" : "#version 330 core\n";
        _lit = new ShaderProgram(gl, header, Shaders.LitVertex, Shaders.LitFragment);
        _beam = new ShaderProgram(gl, header, Shaders.BeamVertex, Shaders.BeamFragment);
        _glow = new ShaderProgram(gl, header, Shaders.GlowVertex, Shaders.GlowFragment);
        _box = new GpuMesh(gl, MeshBuilder.UnitBox());
        _quad = new GpuMesh(gl, MeshBuilder.UnitQuad());
        _cylinder = new GpuMesh(gl, MeshBuilder.UnitCylinder());
        _cone = new GpuMesh(gl, MeshBuilder.Cone());
        gl.GetInteger(GLEnum.MaxSamples, out int maxSamples);
        _maxSamples = maxSamples;
        _scene = new OffscreenTarget(gl);
        _resolve = new OffscreenTarget(gl);
        Info = $"{gl.GetStringS(StringName.Renderer)} · {gl.GetStringS(StringName.Version)} · up to MSAA {maxSamples}x";
    }

    public string Info { get; }

    public void Render(SceneSnapshot scene, Camera camera, int targetFramebuffer, int width, int height, StageFidelity fidelity)
    {
        var gl = _gl;
        var samples = Math.Min(fidelity.Samples, _maxSamples);
        if (samples < 2)
            samples = 0;
        var scaledWidth = Math.Max(1, (int)Math.Round(width * fidelity.RenderScale));
        var scaledHeight = Math.Max(1, (int)Math.Round(height * fidelity.RenderScale));
        var scaled = scaledWidth != width || scaledHeight != height;
        var offscreen = (samples > 0 || scaled) && _scene.Ensure(scaledWidth, scaledHeight, samples, withDepth: true);
        if (!offscreen)
            (scaledWidth, scaledHeight) = (width, height);
        var drawTarget = offscreen ? _scene.Framebuffer : (uint)targetFramebuffer;
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, drawTarget);
        gl.Viewport(0, 0, (uint)scaledWidth, (uint)scaledHeight);
        gl.ClearColor(0.008f, 0.009f, 0.012f, 1);
        gl.DepthMask(true);
        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        gl.Enable(EnableCap.DepthTest);
        gl.DepthFunc(DepthFunction.Lequal);
        gl.Disable(EnableCap.Blend);
        gl.Disable(EnableCap.CullFace);

        var view = camera.View();
        var proj = camera.Projection(width / (float)Math.Max(1, height));
        UploadVideo(scene.Video);

        DrawSolids(scene, camera, view, proj, fidelity.MaxLights);
        DrawLightEffects(scene, camera, view, proj, fidelity.HazeSteps);

        gl.DepthMask(true);
        gl.Disable(EnableCap.Blend);
        gl.Disable(EnableCap.DepthTest);
        gl.BindVertexArray(0);
        gl.UseProgram(0);

        if (offscreen)
            Present(targetFramebuffer, width, height, scaledWidth, scaledHeight, samples > 0 && scaled);
    }

    /// <summary>Copies the offscreen picture onto Avalonia's surface, resolving MSAA and scaling up as needed.</summary>
    private void Present(int target, int width, int height, int scaledWidth, int scaledHeight, bool resolveFirst)
    {
        var gl = _gl;
        var source = _scene.Framebuffer;
        if (resolveFirst && _resolve.Ensure(scaledWidth, scaledHeight, samples: 0, withDepth: false))
        {
            Blit(source, _resolve.Framebuffer, scaledWidth, scaledHeight, scaledWidth, scaledHeight, BlitFramebufferFilter.Nearest);
            source = _resolve.Framebuffer;
        }
        var filter = scaledWidth == width && scaledHeight == height ? BlitFramebufferFilter.Nearest : BlitFramebufferFilter.Linear;
        Blit(source, (uint)target, scaledWidth, scaledHeight, width, height, filter);
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)target);
    }

    private void Blit(uint from, uint to, int fromWidth, int fromHeight, int toWidth, int toHeight, BlitFramebufferFilter filter)
    {
        _gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, from);
        _gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, to);
        _gl.BlitFramebuffer(0, 0, fromWidth, fromHeight, 0, 0, toWidth, toHeight, ClearBufferMask.ColorBufferBit, filter);
    }

    private void DrawSolids(SceneSnapshot scene, Camera camera, Matrix4x4 view, Matrix4x4 proj, int maxLights)
    {
        var lit = _lit;
        lit.Use();
        lit.Set("uView", view);
        lit.Set("uProj", proj);
        lit.Set("uCamPos", camera.Position);
        lit.Set("uAmbient", scene.Ambient);
        lit.Set("uBounce", scene.Bounce);
        lit.Set("uHaze", scene.Haze);
        lit.Set("uExposure", 1.1f);
        lit.Set("uGrid", scene.GridStrength);
        lit.Set("uVideo", 0);
        lit.Set("uHasVideo", _hasVideo ? 1 : 0);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _videoTexture);
        UploadLights(scene, maxLights);

        // House floor, a hair below zero so platform bottoms never fight it.
        Material(new Vector3(0.17f, 0.17f, 0.18f), 0.3f, 0, Vector3.Zero, 2);
        lit.Set("uModel", Matrix4x4.CreateScale(120, 120, 1) * Matrix4x4.CreateRotationX(-MathF.PI / 2) * Matrix4x4.CreateTranslation(0, -0.002f, 0));
        _quad.Draw();

        foreach (var obj in scene.Objects)
            DrawObject(obj);
        foreach (var fixture in scene.Fixtures)
            DrawFixture(fixture);
    }

    private void UploadLights(SceneSnapshot scene, int maxLights)
    {
        var limit = Math.Clamp(maxLights, 1, Shaders.MaxLights);
        var lights = scene.Fixtures.Where(f => f.Brightness > 0.004f).OrderByDescending(f => f.Brightness).Take(limit).ToList();
        Span<Vector3> pos = stackalloc Vector3[Shaders.MaxLights];
        Span<Vector3> dir = stackalloc Vector3[Shaders.MaxLights];
        Span<Vector3> col = stackalloc Vector3[Shaders.MaxLights];
        Span<Vector2> cone = stackalloc Vector2[Shaders.MaxLights];
        for (var i = 0; i < lights.Count; i++)
        {
            var f = lights[i];
            var half = f.BeamAngle * 0.5f * MathF.PI / 180;
            pos[i] = f.Lens;
            dir[i] = f.BeamDirection;
            col[i] = f.Light * Power(f);
            cone[i] = new Vector2(MathF.Cos(MathF.Min(1.5f, half * 1.15f)), MathF.Cos(half * 0.55f));
        }
        _lit.Set("uLightCount", lights.Count);
        _lit.SetArray("uLightPos", pos);
        _lit.SetArray("uLightDir", dir);
        _lit.SetArray("uLightColor", col);
        _lit.SetArray("uLightCone", cone);
    }

    // The same lamp spread over a narrower beam lands brighter; this keeps spots punchy and washes soft.
    private static float Power(FixtureVisual f) => 22f * MathF.Pow(25f / Math.Clamp(f.BeamAngle, 4, 120), 0.9f);

    private void Material(Vector3 albedo, float roughness, float metallic, Vector3 emissive, int pattern)
    {
        _lit.Set("uAlbedo", albedo);
        _lit.Set("uRoughness", roughness);
        _lit.Set("uMetallic", metallic);
        _lit.Set("uEmissive", emissive);
        _lit.Set("uPattern", pattern);
    }

    private static readonly Vector3 SelectGlow = new(0.05f, 0.22f, 0.55f);

    private void DrawObject(ObjectVisual obj)
    {
        var place = Matrix4x4.CreateRotationY(obj.Yaw * MathF.PI / 180) * Matrix4x4.CreateTranslation(obj.Position);
        var emissive = obj.Selected ? SelectGlow : Vector3.Zero;
        switch (obj.Kind)
        {
            case StageObjectKind.Truss:
                Material(obj.Color, 0.35f, 0.9f, emissive, 4);
                _lit.Set("uModel", place);
                TrussMesh(obj.Size).Draw();
                break;
            case StageObjectKind.Wall:
                Material(obj.Color, 0.9f, 0, emissive, 3);
                _lit.Set("uModel", Matrix4x4.CreateScale(obj.Size) * place);
                _box.Draw();
                break;
            case StageObjectKind.Screen:
                Material(new Vector3(0.03f), 0.5f, 0.3f, emissive, 0);
                _lit.Set("uModel", Matrix4x4.CreateScale(obj.Size) * place);
                _box.Draw();
                Material(Vector3.One, 1, 0, Vector3.Zero, 5);
                var face = Matrix4x4.CreateScale(obj.Size.X * 0.97f, obj.Size.Y * 0.95f, 1) * Matrix4x4.CreateTranslation(0, 0, obj.Size.Z / 2 + 0.003f);
                _lit.Set("uModel", face * place);
                _quad.Draw();
                break;
            default:
                Material(obj.Color, 0.5f, 0, emissive, 1);
                _lit.Set("uModel", Matrix4x4.CreateScale(obj.Size) * place);
                _box.Draw();
                break;
        }
    }

    private GpuMesh TrussMesh(Vector3 size)
    {
        var key = ((int)(size.X * 1000), (int)(size.Y * 1000), (int)(size.Z * 1000));
        if (!_trusses.TryGetValue(key, out var mesh))
            _trusses[key] = mesh = new GpuMesh(_gl, MeshBuilder.Truss(size.X, size.Y, size.Z));
        return mesh;
    }

    /// <summary>Rotation taking +Z onto <paramref name="forward"/>, keeping the local up as close to world up as it can.</summary>
    public static Matrix4x4 Facing(Vector3 forward)
    {
        var z = Vector3.Normalize(forward);
        var helper = MathF.Abs(z.Y) > 0.98f ? Vector3.UnitX : Vector3.UnitY;
        var x = Vector3.Normalize(Vector3.Cross(helper, z));
        var y = Vector3.Cross(z, x);
        return new Matrix4x4(x.X, x.Y, x.Z, 0, y.X, y.Y, y.Z, 0, z.X, z.Y, z.Z, 0, 0, 0, 0, 1);
    }

    private void DrawFixture(FixtureVisual f)
    {
        var face = Facing(f.BeamDirection);
        var at = Matrix4x4.CreateTranslation(f.Position);
        var body = new Vector3(0.05f, 0.05f, 0.055f);
        var emissive = f.Selected ? SelectGlow : Vector3.Zero;
        var lens = f.Light * 6f + new Vector3(0.01f);
        Material(body, 0.45f, 0.4f, emissive, 0);

        switch (f.Kind)
        {
            case BeamKind.Bar:
                _lit.Set("uModel", Matrix4x4.CreateScale(0.9f, 0.08f, 0.1f) * face * at);
                _box.Draw();
                Material(Vector3.Zero, 1, 0, lens, 0);
                _lit.Set("uModel", Matrix4x4.CreateScale(0.86f, 0.05f, 0.01f) * Matrix4x4.CreateTranslation(0, 0, 0.051f) * face * at);
                _box.Draw();
                return;
            case BeamKind.Strobe or BeamKind.Blinder:
                _lit.Set("uModel", Matrix4x4.CreateScale(0.46f, 0.22f, 0.14f) * face * at);
                _box.Draw();
                Material(Vector3.Zero, 1, 0, lens, 0);
                _lit.Set("uModel", Matrix4x4.CreateScale(0.42f, 0.18f, 0.01f) * Matrix4x4.CreateTranslation(0, 0, 0.071f) * face * at);
                _box.Draw();
                return;
        }

        if (f.MovingHead)
        {
            // Base on the truss, a yoke turning with pan, and the head pointing along the beam.
            _lit.Set("uModel", Matrix4x4.CreateScale(0.36f, 0.13f, 0.3f) * Matrix4x4.CreateTranslation(0, 0.26f, 0) * at);
            _box.Draw();
            var panYaw = MathF.Atan2(f.BeamDirection.X, f.BeamDirection.Z);
            var yoke = Matrix4x4.CreateRotationY(panYaw) * at;
            _lit.Set("uModel", Matrix4x4.CreateScale(0.4f, 0.04f, 0.08f) * Matrix4x4.CreateTranslation(0, 0.18f, 0) * yoke);
            _box.Draw();
            foreach (var side in new[] { -0.19f, 0.19f })
            {
                _lit.Set("uModel", Matrix4x4.CreateScale(0.035f, 0.22f, 0.08f) * Matrix4x4.CreateTranslation(side, 0.07f, 0) * yoke);
                _box.Draw();
            }
            _lit.Set("uModel", Matrix4x4.CreateScale(0.13f, 0.13f, 0.34f) * Matrix4x4.CreateTranslation(0, 0, -0.17f) * face * at);
            _cylinder.Draw();
        }
        else
        {
            var radius = f.Kind == BeamKind.Wash ? 0.16f : 0.12f;
            _lit.Set("uModel", Matrix4x4.CreateScale(0.03f, 0.2f, 0.03f) * Matrix4x4.CreateTranslation(0, 0.2f, 0) * at);
            _box.Draw();
            _lit.Set("uModel", Matrix4x4.CreateScale(radius, radius, 0.3f) * Matrix4x4.CreateTranslation(0, 0, -0.15f) * face * at);
            _cylinder.Draw();
        }
        Material(Vector3.Zero, 1, 0, lens, 0);
        var lensRadius = f.MovingHead ? 0.1f : f.Kind == BeamKind.Wash ? 0.14f : 0.1f;
        _lit.Set("uModel", Matrix4x4.CreateScale(lensRadius, lensRadius, 0.01f) * Matrix4x4.CreateTranslation(0, 0, f.LensOffset - 0.005f) * face * at);
        _cylinder.Draw();
    }

    private void DrawLightEffects(SceneSnapshot scene, Camera camera, Matrix4x4 view, Matrix4x4 proj, int hazeSteps)
    {
        var gl = _gl;
        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(BlendingFactor.One, BlendingFactor.One);
        gl.DepthMask(false);

        if (scene.Haze > 0.001f)
        {
            _beam.Use();
            _beam.Set("uView", view);
            _beam.Set("uProj", proj);
            _beam.Set("uCamPos", camera.Position);
            _beam.Set("uHaze", scene.Haze);
            _beam.Set("uTime", scene.Time);
            _beam.Set("uSteps", hazeSteps);
            foreach (var f in scene.Fixtures)
            {
                if (f.Brightness < 0.01f || f.BeamLength < 0.05f)
                    continue;
                var half = Math.Clamp(f.BeamAngle, 2, 150) * 0.5f * MathF.PI / 180;
                var radius = f.BeamLength * MathF.Tan(half);
                var model = Matrix4x4.CreateScale(radius * 1.04f, radius * 1.04f, f.BeamLength) * Facing(f.BeamDirection) * Matrix4x4.CreateTranslation(f.Lens);
                _beam.Set("uModel", model);
                _beam.Set("uApex", f.Lens);
                _beam.Set("uAxis", f.BeamDirection);
                _beam.Set("uCos", MathF.Cos(half));
                _beam.Set("uLength", f.BeamLength);
                // Wide washes spread the same light thin; keep them from fogging out the whole view.
                _beam.Set("uColor", f.Light * Power(f) * 0.12f);
                _cone.Draw();
            }
        }

        _glow.Use();
        _glow.Set("uView", view);
        _glow.Set("uProj", proj);
        foreach (var f in scene.Fixtures)
        {
            if (f.Brightness < 0.01f)
                continue;
            var toCamera = Vector3.Normalize(camera.Position - f.Lens);
            var facing = MathF.Pow(MathF.Max(0, Vector3.Dot(toCamera, f.BeamDirection)), 6);
            _glow.Set("uCenter", f.Lens + toCamera * 0.05f);
            _glow.Set("uSize", 0.35f + 0.9f * facing);
            _glow.Set("uFacing", facing);
            _glow.Set("uColor", f.Light * 1.6f);
            _quad.Draw();
        }
    }

    private void UploadVideo(VideoFrame? frame)
    {
        if (frame is null || frame.Width == 0)
        {
            _hasVideo = false;
            return;
        }
        if (_videoTexture == 0)
        {
            _videoTexture = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, _videoTexture);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        }
        _hasVideo = true;
        if (frame.Version == _videoVersion)
            return;
        lock (frame)
        {
            _videoVersion = frame.Version;
            _gl.BindTexture(TextureTarget.Texture2D, _videoTexture);
            // Uploaded as RGBA and swizzled to .bgr in the shader: GLES has no portable BGRA upload.
            fixed (byte* p = frame.Pixels)
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)frame.Width, (uint)frame.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
        }
    }

    public void Dispose()
    {
        _scene.Dispose();
        _resolve.Dispose();
        if (_videoTexture != 0)
            _gl.DeleteTexture(_videoTexture);
        foreach (var mesh in _trusses.Values)
            mesh.Dispose();
        _box.Dispose();
        _quad.Dispose();
        _cylinder.Dispose();
        _cone.Dispose();
        _lit.Dispose();
        _beam.Dispose();
        _glow.Dispose();
    }
}
