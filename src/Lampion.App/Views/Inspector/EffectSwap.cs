using Lampion.Core.Model.Effects;
using Lampion.Core.Model.Timeline;
using LightEffect = Lampion.Core.Model.Effects.Effect;

namespace Lampion.App.Views.Inspector;

/// <summary>Replaces a clip's effect from Effect Controls (change effect, reset). Doesn't notify; wrap it in an edit.</summary>
internal static class EffectSwap
{
    /// <summary>Puts <paramref name="replacement"/> on <paramref name="clip"/>; a clip still named after its old effect is renamed.</summary>
    /// <param name="clip">The clip to change.</param>
    /// <param name="replacement">A fresh effect, owned by the clip from now on.</param>
    /// <param name="keepLook">Carries over the intensity and, between single-colour effects, the colour the user picked.</param>
    public static void Replace(LightClip clip, LightEffect replacement, bool keepLook)
    {
        var old = clip.Effect;
        if (keepLook)
        {
            replacement.Intensity = old.Intensity;
            SetMainColor(replacement, old.SwatchColor);
        }
        if (clip.Name == old.DisplayName)
            clip.Name = replacement.DisplayName;
        clip.Effect = replacement;
    }

    // The effects with a single "Color" parameter; palettes and two-colour effects keep their defaults.
    private static void SetMainColor(LightEffect effect, string hex)
    {
        switch (effect)
        {
            case SolidEffect e:
                e.Color = hex;
                break;
            case StrobeEffect e:
                e.Color = hex;
                break;
            case PulseEffect e:
                e.Color = hex;
                break;
            case EvenSparkleEffect e:
                e.Color = hex;
                break;
            case SparkleEffect e:
                e.Color = hex;
                break;
            case ColorWaveEffect e:
                e.Color = hex;
                break;
            case LightningEffect e:
                e.Color = hex;
                break;
            case HeartbeatEffect e:
                e.Color = hex;
                break;
            case HitEffect e:
                e.Color = hex;
                break;
        }
    }
}
