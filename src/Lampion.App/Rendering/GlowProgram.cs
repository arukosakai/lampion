using System.Numerics;
using Silk.NET.OpenGL;

namespace Lampion.App.Rendering;

/// <summary>Camera-facing lens glow: a soft halo plus a horizontal streak when the lens looks at the viewer.</summary>
internal sealed class GlowProgram : IDisposable
{
    private const string VertexSource = """
        layout(location = 0) in vec3 aPos;
        layout(location = 2) in vec2 aUv;
        uniform mat4 uView;
        uniform mat4 uProj;
        uniform vec3 uCenter;
        uniform float uSize;
        out vec2 vUv;
        void main() {
            vec4 center = uView * vec4(uCenter, 1.0);
            center.xy += aPos.xy * uSize;
            vUv = aUv;
            gl_Position = uProj * center;
        }
        """;

    private static readonly string FragmentSource = Shaders.Common + """

        in vec2 vUv;
        out vec4 fragColor;
        uniform vec3 uColor;
        uniform float uFacing;
        void main() {
            vec2 p = vUv * 2.0 - 1.0;
            float r = length(p);
            float halo = exp(-r * r * 6.0) * 0.8 + exp(-r * 14.0) * 1.5;
            // A faint horizontal streak when the lens looks right at the camera.
            float streak = exp(-abs(p.y) * 60.0) * (1.0 - smoothstep(0.0, 1.0, abs(p.x))) * uFacing;
            vec3 c = uColor * (halo * (0.25 + uFacing) + streak * 0.6);
            fragColor = vec4(1.0 - exp(-c), 1.0);
        }
        """;

    private readonly ShaderProgram _program;
    private readonly int _view;
    private readonly int _proj;
    private readonly int _center;
    private readonly int _size;
    private readonly int _facing;
    private readonly int _color;

    /// <summary>Compiles the program and resolves its uniforms; the GL context must be current.</summary>
    /// <exception cref="InvalidOperationException">The driver rejects the shader.</exception>
    public GlowProgram(GL gl, string header)
    {
        _program = new ShaderProgram(gl, header, VertexSource, FragmentSource);
        _view = _program.Location("uView");
        _proj = _program.Location("uProj");
        _center = _program.Location("uCenter");
        _size = _program.Location("uSize");
        _facing = _program.Location("uFacing");
        _color = _program.Location("uColor");
    }

    /// <summary>Makes this the current program; every setter below needs it current.</summary>
    public void Use() => _program.Use();

    /// <summary>Camera matrices for the frame.</summary>
    public void SetCamera(in Matrix4x4 view, in Matrix4x4 projection)
    {
        _program.Set(_view, in view);
        _program.Set(_proj, in projection);
    }

    /// <summary>One glow for the next draw of the unit quad.</summary>
    /// <param name="center">World position of the glow, metres.</param>
    /// <param name="size">Width of the billboard (the unit quad scaled), in view-space metres.</param>
    /// <param name="facing">0..1, how squarely the lens faces the camera.</param>
    /// <param name="color">Glow colour, linear RGB.</param>
    public void SetGlow(Vector3 center, float size, float facing, Vector3 color)
    {
        _program.Set(_center, center);
        _program.Set(_size, size);
        _program.Set(_facing, facing);
        _program.Set(_color, color);
    }

    /// <summary>Deletes the program.</summary>
    public void Dispose() => _program.Dispose();
}
