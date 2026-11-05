using System.Numerics;
using Silk.NET.OpenGL;

namespace Lampion.App.Rendering;

/// <summary>
/// Volumetric beam. The cone mesh only bounds the work: each fragment intersects the view ray with the
/// exact cone analytically, then ray-marches the in-scattered light through animated haze. Both faces
/// are rasterised; the fragment on the near half of the chord is discarded so each pixel integrates once
/// whichever way the mesh is wound, and from inside the beam too.
/// </summary>
internal sealed class BeamProgram : IDisposable
{
    private const string VertexSource = """
        layout(location = 0) in vec3 aPos;
        uniform mat4 uModel;
        uniform mat4 uView;
        uniform mat4 uProj;
        out vec3 vWorld;
        void main() {
            vec4 world = uModel * vec4(aPos, 1.0);
            vWorld = world.xyz;
            gl_Position = uProj * uView * world;
        }
        """;

    private static readonly string FragmentSource = Shaders.Common + $$"""

        in vec3 vWorld;
        out vec4 fragColor;
        uniform vec3 uCamPos;
        uniform vec3 uApex;
        uniform vec3 uAxis;
        uniform float uCos;
        uniform float uLength;
        uniform vec3 uColor;
        uniform float uHaze;
        uniform float uTime;
        uniform int uSteps;

        void main() {
            vec3 ro = uCamPos;
            vec3 rd = normalize(vWorld - uCamPos);
            float fragT = length(vWorld - uCamPos);

            vec3 co = ro - uApex;
            float dv = dot(rd, uAxis);
            float cv = dot(co, uAxis);
            float cos2 = uCos * uCos;
            float a = dv * dv - cos2;
            float b = 2.0 * (dv * cv - dot(rd, co) * cos2);
            float c = cv * cv - dot(co, co) * cos2;

            float t0 = -1e9;
            float t1 = 1e9;
            if (abs(a) < 1e-6) {
                float t = -c / b;
                if (dv > 0.0) t0 = t; else t1 = t;
            } else {
                float disc = b * b - 4.0 * a * c;
                if (disc < 0.0) discard;
                float s = sqrt(disc);
                float r0 = (-b - s) / (2.0 * a);
                float r1 = (-b + s) / (2.0 * a);
                if (r0 > r1) { float tmp = r0; r0 = r1; r1 = tmp; }
                if (a < 0.0) {
                    t0 = r0; t1 = r1;
                    if (cv + dv * 0.5 * (r0 + r1) < 0.0) discard;   // that is the mirror nappe
                } else {
                    if (cv + dv * r1 >= 0.0 && dv > 0.0) { t0 = r1; }
                    else { t1 = r0; }
                }
            }
            // Clip to the beam's length along the axis (h = cv + t*dv in [0, length]).
            if (abs(dv) > 1e-6) {
                float ha = (0.0 - cv) / dv;
                float hb = (uLength - cv) / dv;
                t0 = max(t0, min(ha, hb));
                t1 = min(t1, max(ha, hb));
            } else if (cv < 0.0 || cv > uLength) discard;
            t0 = max(t0, 0.0);
            if (t1 <= t0) discard;
            if (fragT < 0.5 * (t0 + t1)) discard;

            // Fewer steps only make the haze grainier, not dimmer: the sum is scaled by the step length.
            const int MAX_STEPS = {{Shaders.MaxHazeSteps}};
            int steps = clamp(uSteps, 1, MAX_STEPS);
            float stepLen = (t1 - t0) / float(steps);
            float jitter = hash13(vec3(gl_FragCoord.xy, uTime));
            float tanAngle = sqrt(max(1.0 - cos2, 1e-6)) / uCos;
            float sum = 0.0;
            for (int i = 0; i < MAX_STEPS; i++) {
                if (i >= steps) break;
                float t = t0 + (float(i) + jitter) * stepLen;
                vec3 p = ro + rd * t;
                vec3 q = p - uApex;
                float h = dot(q, uAxis);
                float r = length(q - uAxis * h) / max(h * tanAngle, 1e-4);
                float edge = 1.0 - smoothstep(0.55, 1.0, r);
                float core = 1.0 + 1.5 * (1.0 - smoothstep(0.0, 0.4, r));
                // Same power spread over a growing disc: brightness falls with the square of the throw.
                float spread = 1.0 / (0.15 + h * h * 0.09);
                vec3 drift = vec3(uTime * 0.12, uTime * 0.05, -uTime * 0.08);
                float haze = 0.35 + 0.9 * fbm(p * 0.9 + drift);
                sum += edge * core * spread * haze;
            }
            vec3 light = uColor * sum * stepLen * uHaze * 0.22;
            fragColor = vec4(1.0 - exp(-light), 1.0);
        }
        """;

    private readonly ShaderProgram _program;
    private readonly int _model;
    private readonly int _view;
    private readonly int _proj;
    private readonly int _camPos;
    private readonly int _haze;
    private readonly int _time;
    private readonly int _steps;
    private readonly int _apex;
    private readonly int _axis;
    private readonly int _cos;
    private readonly int _length;
    private readonly int _color;

    /// <summary>Compiles the program and resolves its uniforms; the GL context must be current.</summary>
    /// <exception cref="InvalidOperationException">The driver rejects the shader.</exception>
    public BeamProgram(GL gl, string header)
    {
        _program = new ShaderProgram(gl, header, VertexSource, FragmentSource);
        _model = _program.Location("uModel");
        _view = _program.Location("uView");
        _proj = _program.Location("uProj");
        _camPos = _program.Location("uCamPos");
        _haze = _program.Location("uHaze");
        _time = _program.Location("uTime");
        _steps = _program.Location("uSteps");
        _apex = _program.Location("uApex");
        _axis = _program.Location("uAxis");
        _cos = _program.Location("uCos");
        _length = _program.Location("uLength");
        _color = _program.Location("uColor");
    }

    /// <summary>Makes this the current program; every setter below needs it current.</summary>
    public void Use() => _program.Use();

    /// <summary>Per-frame uniforms.</summary>
    /// <param name="view">World-to-view matrix.</param>
    /// <param name="projection">View-to-clip matrix.</param>
    /// <param name="eye">Camera position, metres.</param>
    /// <param name="haze">Haze density 0..1.</param>
    /// <param name="time">Seconds, for drifting haze and per-frame jitter.</param>
    /// <param name="steps">Ray-march steps, clamped to 1..<see cref="Shaders.MaxHazeSteps"/> by the shader.</param>
    public void SetFrame(in Matrix4x4 view, in Matrix4x4 projection, Vector3 eye, float haze, float time, int steps)
    {
        _program.Set(_view, in view);
        _program.Set(_proj, in projection);
        _program.Set(_camPos, eye);
        _program.Set(_haze, haze);
        _program.Set(_time, time);
        _program.Set(_steps, steps);
    }

    /// <summary>One beam's cone for the next draw.</summary>
    /// <param name="model">Places the unit cone mesh around the beam.</param>
    /// <param name="apex">Lens position, metres.</param>
    /// <param name="axis">Unit beam direction.</param>
    /// <param name="cosHalfAngle">Cosine of half the cone angle.</param>
    /// <param name="length">Metres to where the beam lands.</param>
    /// <param name="color">Scattered light, linear RGB.</param>
    public void SetBeam(in Matrix4x4 model, Vector3 apex, Vector3 axis, float cosHalfAngle, float length, Vector3 color)
    {
        _program.Set(_model, in model);
        _program.Set(_apex, apex);
        _program.Set(_axis, axis);
        _program.Set(_cos, cosHalfAngle);
        _program.Set(_length, length);
        _program.Set(_color, color);
    }

    /// <summary>Deletes the program.</summary>
    public void Dispose() => _program.Dispose();
}
