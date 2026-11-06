namespace Lampion.App.Rendering;

/// <summary>
/// What the GLSL programs share. Every source is written to compile as both GLSL 3.30 core (desktop GL)
/// and GLSL ES 3.00 (ANGLE on Windows); <see cref="Header"/> picks the matching #version line.
/// </summary>
internal static class Shaders
{
    /// <summary>Most fixtures that light a surface; sizes the lit shader's light arrays.</summary>
    public const int MaxLights = 48;

    /// <summary>Most ray-march steps through a beam; bounds the beam shader's loop.</summary>
    public const int MaxHazeSteps = 28;

    /// <summary>Tonemapping and noise helpers, prepended to the fragment shaders that need them.</summary>
    public const string Common = """
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

        float valueNoise(vec3 p) {
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
            for (int i = 0; i < 3; i++) { v += a * valueNoise(p); p *= 2.03; a *= 0.5; }
            return v;
        }
        """;

    /// <summary>The #version (and precision) lines for the context's GL flavour.</summary>
    /// <param name="isEmbedded">True for OpenGL ES (ANGLE), false for desktop GL.</param>
    public static string Header(bool isEmbedded) =>
        isEmbedded ? "#version 300 es\nprecision highp float;\nprecision highp int;\n" : "#version 330 core\n";
}
