namespace Lampion.App.Rendering;

/// <summary>
/// GLSL sources, written to compile as both GLSL 3.30 core (desktop GL) and GLSL ES 3.00 (ANGLE on
/// Windows); <see cref="StageRenderer"/> prepends the matching #version line.
/// </summary>
internal static class Shaders
{
    public const int MaxLights = 48;

    private const string Common = """
        const float PI = 3.14159265;

        vec3 tonemap(vec3 c, float exposure) {
            // ACES filmic fit (Narkowicz), then sRGB-ish gamma.
            c *= exposure;
            c = clamp((c * (2.51 * c + 0.03)) / (c * (2.43 * c + 0.59) + 0.14), 0.0, 1.0);
            return pow(c, vec3(1.0 / 2.2));
        }

        float hash13(vec3 p) {
            p = fract(p * 0.1031);
            p += dot(p, p.zyx + 31.32);
            return fract((p.x + p.y) * p.z);
        }

        float noise3(vec3 p) {
            vec3 i = floor(p);
            vec3 f = fract(p);
            f = f * f * (3.0 - 2.0 * f);
            return mix(
                mix(mix(hash13(i), hash13(i + vec3(1, 0, 0)), f.x), mix(hash13(i + vec3(0, 1, 0)), hash13(i + vec3(1, 1, 0)), f.x), f.y),
                mix(mix(hash13(i + vec3(0, 0, 1)), hash13(i + vec3(1, 0, 1)), f.x), mix(hash13(i + vec3(0, 1, 1)), hash13(i + vec3(1, 1, 1)), f.x), f.y),
                f.z);
        }

        float fbm(vec3 p) {
            float v = 0.0;
            float a = 0.5;
            for (int i = 0; i < 3; i++) { v += a * noise3(p); p *= 2.03; a *= 0.5; }
            return v;
        }
        """;

    public const string LitVertex = """
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

    public static readonly string LitFragment = Common + $$"""

        #define MAX_LIGHTS {{MaxLights}}
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

    public const string BeamVertex = """
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

    /// <summary>
    /// Volumetric beam. The cone mesh only bounds the work: each fragment intersects the view ray with the
    /// exact cone analytically, then ray-marches the in-scattered light through animated haze. Both faces
    /// are rasterised; the fragment on the near half of the chord is discarded so each pixel integrates once
    /// whichever way the mesh is wound, and from inside the beam too.
    /// </summary>
    public static readonly string BeamFragment = Common + """

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

            const int STEPS = 28;
            float stepLen = (t1 - t0) / float(STEPS);
            float jitter = hash13(vec3(gl_FragCoord.xy, uTime));
            float tanAngle = sqrt(max(1.0 - cos2, 1e-6)) / uCos;
            float sum = 0.0;
            for (int i = 0; i < STEPS; i++) {
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

    public const string GlowVertex = """
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

    public static readonly string GlowFragment = Common + """

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
}
