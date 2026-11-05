using System.Numerics;
using Silk.NET.OpenGL;

namespace Lampion.App.Rendering;

/// <summary>
/// The physically-inspired surface shader for the set, the floor and fixture bodies: spot lights with a
/// soft cone, house light, room bounce, haze fog and ACES tonemapping. Wraps its uniforms by location.
/// </summary>
internal sealed class LitProgram : IDisposable
{
    private const string VertexSource = """
        layout(location = 0) in vec3 aPos;
        layout(location = 1) in vec3 aNormal;
        layout(location = 2) in vec2 aUv;
        uniform mat4 uModel;
        uniform mat4 uView;
        uniform mat4 uProj;
        out vec3 vWorld;
        out vec3 vNormal;
        out vec2 vUv;
        void main() {
            vec4 world = uModel * vec4(aPos, 1.0);
            vWorld = world.xyz;
            // Only boxes get non-uniform scale, and that keeps their face normals axis-aligned, so
            // renormalising in the fragment shader stands in for a proper normal matrix.
            vNormal = mat3(uModel) * aNormal;
            vUv = aUv;
            gl_Position = uProj * uView * world;
        }
        """;

    private static readonly string FragmentSource = Shaders.Common + $$"""

        #define MAX_LIGHTS {{Shaders.MaxLights}}
        in vec3 vWorld;
        in vec3 vNormal;
        in vec2 vUv;
        out vec4 fragColor;

        uniform int uLightCount;
        uniform vec3 uLightPos[MAX_LIGHTS];
        uniform vec3 uLightDir[MAX_LIGHTS];
        uniform vec3 uLightColor[MAX_LIGHTS];
        uniform vec2 uLightCone[MAX_LIGHTS];   // cos(outer), cos(inner)

        uniform vec3 uCamPos;
        uniform vec3 uAlbedo;
        uniform float uRoughness;
        uniform float uMetallic;
        uniform vec3 uEmissive;
        uniform int uPattern;      // 0 plain, 1 deck, 2 house floor, 3 drape, 4 brushed metal, 5 video
        uniform float uAmbient;
        uniform vec3 uBounce;      // light spilled into the room by the rig, when enabled
        uniform float uHaze;
        uniform float uExposure;
        uniform float uGrid;       // builder grid strength
        uniform sampler2D uVideo;
        uniform int uHasVideo;

        float gridLine(vec2 p, float step, float width) {
            vec2 g = abs(fract(p / step - 0.5) - 0.5) * step;
            vec2 aa = fwidth(p) * 1.2;
            vec2 l = 1.0 - smoothstep(vec2(width) - aa, vec2(width) + aa, g);
            return max(l.x, l.y);
        }

        void surface(inout vec3 albedo, inout float rough, inout vec3 n) {
            if (uPattern == 1) {
                // Stage deck: 1.22 m sheets of painted ply, slightly varied per sheet, with dark seams.
                vec2 p = vWorld.xz;
                vec2 sheet = floor(p / vec2(1.22, 2.44));
                float tone = 0.85 + 0.3 * hash13(vec3(sheet, 3.0));
                float seam = gridLine(p, 1.22, 0.004) * step(0.5, n.y);
                albedo *= tone * (1.0 - 0.6 * seam);
                rough = mix(0.35, 0.6, noise3(vec3(p * 6.0, 1.0)));
            } else if (uPattern == 2) {
                // Polished concrete with a metre grid so distances can be read off the floor.
                vec2 p = vWorld.xz;
                float blotch = fbm(vec3(p * 0.7, 0.0));
                albedo *= 0.75 + 0.5 * blotch;
                rough = mix(0.18, 0.45, blotch);
                float metre = gridLine(p, 1.0, 0.008);
                float five = gridLine(p, 5.0, 0.016);
                albedo = mix(albedo, vec3(0.32, 0.36, 0.42), (metre * 0.35 + five * 0.6) * uGrid);
            } else if (uPattern == 3) {
                // Theatrical drape: vertical folds bend the normal and shade the troughs.
                float fold = sin(vWorld.x * 9.0 + sin(vWorld.x * 2.3) * 2.0);
                n = normalize(n + vec3(fold * 0.35, 0.0, 0.0));
                albedo *= 0.8 + 0.2 * fold;
                rough = 0.9;
            } else if (uPattern == 4) {
                rough = 0.3 + 0.15 * noise3(vWorld * 40.0);
            }
        }

        void main() {
            vec3 n = normalize(vNormal);
            vec3 v = normalize(uCamPos - vWorld);
            if (dot(n, v) < 0.0) n = -n;
            vec3 albedo = uAlbedo;
            float rough = uRoughness;
            surface(albedo, rough, n);

            if (uPattern == 5) {
                vec3 video = uHasVideo == 1 ? texture(uVideo, vec2(vUv.x, 1.0 - vUv.y)).bgr : vec3(0.02);
                // An LED wall is a light source: show it at full brightness, a touch of scan-line texture.
                float pixel = 0.9 + 0.1 * gridLine(vUv * vec2(160.0, 90.0), 1.0, 0.08);
                fragColor = vec4(video * pixel, 1.0);
                return;
            }

            float shininess = mix(512.0, 8.0, rough);
            float specScale = mix(0.04, 0.9, uMetallic) * (1.0 - rough * 0.7);
            vec3 diffuseColor = albedo * (1.0 - uMetallic);
            vec3 color = vec3(0.0);

            for (int i = 0; i < MAX_LIGHTS; i++) {
                if (i >= uLightCount) break;
                vec3 toLight = uLightPos[i] - vWorld;
                float dist = length(toLight);
                vec3 l = toLight / dist;
                float cosAngle = dot(-l, uLightDir[i]);
                float cone = smoothstep(uLightCone[i].x, uLightCone[i].y, cosAngle);
                if (cone <= 0.0) continue;
                // A soft hot-spot in the middle of the field, as real optics give.
                float hotspot = 1.0 + 0.6 * smoothstep(uLightCone[i].y, 1.0, cosAngle);
                float atten = 1.0 / (1.0 + 0.02 * dist * dist);
                float ndl = max(dot(n, l), 0.0);
                vec3 h = normalize(l + v);
                float spec = pow(max(dot(n, h), 0.0), shininess) * specScale * (shininess + 8.0) / (8.0 * PI);
                vec3 radiance = uLightColor[i] * cone * hotspot * atten;
                color += radiance * ndl * (diffuseColor + spec);
            }

            // House light: a cool sky above, warm bounce from below.
            vec3 hemi = mix(vec3(0.05, 0.045, 0.04), vec3(0.09, 0.1, 0.13), n.y * 0.5 + 0.5);
            color += albedo * hemi * uAmbient * 14.0;
            color += albedo * uBounce * (0.6 + 0.4 * (n.y * 0.5 + 0.5));
            color += uEmissive;

            // Haze swallows distant surfaces into the dark room.
            float d = length(uCamPos - vWorld);
            color = mix(color, vec3(0.008, 0.009, 0.012), 1.0 - exp(-d * 0.012 * uHaze));

            vec3 mapped = tonemap(color, uExposure);
            mapped += (hash13(vec3(gl_FragCoord.xy, 1.0)) - 0.5) / 255.0;
            fragColor = vec4(mapped, 1.0);
        }
        """;

    private readonly ShaderProgram _program;
    private readonly int _model;
    private readonly int _view;
    private readonly int _proj;
    private readonly int _camPos;
    private readonly int _ambient;
    private readonly int _bounce;
    private readonly int _haze;
    private readonly int _exposure;
    private readonly int _grid;
    private readonly int _video;
    private readonly int _hasVideo;
    private readonly int _albedo;
    private readonly int _roughness;
    private readonly int _metallic;
    private readonly int _emissive;
    private readonly int _pattern;
    private readonly int _lightCount;
    private readonly int _lightPos;
    private readonly int _lightDir;
    private readonly int _lightColor;
    private readonly int _lightCone;

    /// <summary>Compiles the program and resolves its uniforms; the GL context must be current.</summary>
    /// <exception cref="InvalidOperationException">The driver rejects the shader.</exception>
    public LitProgram(GL gl, string header)
    {
        _program = new ShaderProgram(gl, header, VertexSource, FragmentSource);
        _model = _program.Location("uModel");
        _view = _program.Location("uView");
        _proj = _program.Location("uProj");
        _camPos = _program.Location("uCamPos");
        _ambient = _program.Location("uAmbient");
        _bounce = _program.Location("uBounce");
        _haze = _program.Location("uHaze");
        _exposure = _program.Location("uExposure");
        _grid = _program.Location("uGrid");
        _video = _program.Location("uVideo");
        _hasVideo = _program.Location("uHasVideo");
        _albedo = _program.Location("uAlbedo");
        _roughness = _program.Location("uRoughness");
        _metallic = _program.Location("uMetallic");
        _emissive = _program.Location("uEmissive");
        _pattern = _program.Location("uPattern");
        _lightCount = _program.Location("uLightCount");
        _lightPos = _program.Location("uLightPos[0]");
        _lightDir = _program.Location("uLightDir[0]");
        _lightColor = _program.Location("uLightColor[0]");
        _lightCone = _program.Location("uLightCone[0]");
    }

    /// <summary>Makes this the current program; every setter below needs it current.</summary>
    public void Use() => _program.Use();

    /// <summary>Camera matrices and eye position for the frame.</summary>
    public void SetCamera(in Matrix4x4 view, in Matrix4x4 projection, Vector3 eye)
    {
        _program.Set(_view, in view);
        _program.Set(_proj, in projection);
        _program.Set(_camPos, eye);
    }

    /// <summary>Room-wide light for the frame.</summary>
    /// <param name="ambient">House light 0..1.</param>
    /// <param name="bounce">Rig spill, linear RGB.</param>
    /// <param name="haze">Haze density 0..1, which fogs distant surfaces.</param>
    /// <param name="exposure">Tonemapping exposure multiplier.</param>
    public void SetRoom(float ambient, Vector3 bounce, float haze, float exposure)
    {
        _program.Set(_ambient, ambient);
        _program.Set(_bounce, bounce);
        _program.Set(_haze, haze);
        _program.Set(_exposure, exposure);
    }

    /// <summary>Strength (0..1) of the metre grid on the house floor.</summary>
    public void SetGrid(float strength) => _program.Set(_grid, strength);

    /// <summary>Which texture unit holds the LED-screen video, and whether it has a picture.</summary>
    public void SetVideo(int textureUnit, bool hasVideo)
    {
        _program.Set(_video, textureUnit);
        _program.Set(_hasVideo, hasVideo ? 1 : 0);
    }

    /// <summary>Spot lights for the frame; every span must be <see cref="Shaders.MaxLights"/> long, of which the first <paramref name="count"/> are used.</summary>
    public void SetLights(int count, ReadOnlySpan<Vector3> positions, ReadOnlySpan<Vector3> directions, ReadOnlySpan<Vector3> colors, ReadOnlySpan<Vector2> cones)
    {
        _program.Set(_lightCount, count);
        _program.Set(_lightPos, positions);
        _program.Set(_lightDir, directions);
        _program.Set(_lightColor, colors);
        _program.Set(_lightCone, cones);
    }

    /// <summary>Surface material for the next draws.</summary>
    /// <param name="albedo">Base colour, linear RGB.</param>
    /// <param name="roughness">0 mirror .. 1 matte.</param>
    /// <param name="metallic">0 dielectric .. 1 metal.</param>
    /// <param name="emissive">Light the surface gives off itself, linear RGB.</param>
    /// <param name="pattern">Procedural detail.</param>
    public void SetMaterial(Vector3 albedo, float roughness, float metallic, Vector3 emissive, SurfacePattern pattern)
    {
        _program.Set(_albedo, albedo);
        _program.Set(_roughness, roughness);
        _program.Set(_metallic, metallic);
        _program.Set(_emissive, emissive);
        _program.Set(_pattern, (int)pattern);
    }

    /// <summary>Object-to-world matrix for the next draw.</summary>
    public void SetModel(in Matrix4x4 model) => _program.Set(_model, in model);

    /// <summary>Deletes the program.</summary>
    public void Dispose() => _program.Dispose();
}
