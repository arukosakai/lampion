using Lampion.Core.Model.Animation;
using Lampion.Core.Model.Effects;

namespace Lampion.Core.Persistence;

/// <summary>Looks that ship with Lampion, built from the stock effects.</summary>
public static class BuiltInPresets
{
    /// <summary>Fresh instances of every built-in preset; callers own them.</summary>
    public static IReadOnlyList<EffectPreset> All() =>
    [
        new()
        {
            Name = "Police",
            Description = "Red and blue swapping between neighbouring fixtures",
            Duration = 8,
            Effect = new ColorCycleEffect { Colors = ["#FF0010", "#0030FF"], StepTime = 0.25, FadeRatio = 0, FixtureOffset = 1 },
        },
        new()
        {
            Name = "Candlelight",
            Description = "Low, warm, gently flickering",
            Duration = 16,
            FadeIn = 2,
            FadeOut = 2,
            Effect = new FireEffect { Ember = "#7A2400", Flame = "#FF9A3C", Rate = 3, Minimum = 0.55, Intensity = 0.6 },
        },
        new()
        {
            Name = "Ocean",
            Description = "Slow teal and deep-blue swell",
            Duration = 16,
            FadeIn = 3,
            FadeOut = 3,
            FadeEasing = Easing.EaseInOut,
            Effect = new ColorWaveEffect { Color = "#1DE9B6", Base = "#0A1A8C", Rate = 0.15, Wavelength = 6 },
        },
        new()
        {
            Name = "Sunset",
            Description = "Amber to magenta gradient drifting across the rig",
            Duration = 16,
            Effect = new GradientEffect { Colors = ["#FF8C1A", "#FF2A6D", "#7B1FA2"], Speed = 0.05 },
        },
        new()
        {
            Name = "Storm",
            Description = "Scattered lightning strikes, each fixture on its own",
            Duration = 16,
            Effect = new LightningEffect { Color = "#DDE6FF", Rate = 0.8, Mode = LightningMode.Scattered },
        },
        new()
        {
            Name = "Rainbow sweep",
            Description = "Solid red with the hue shift keyframed a full turn round the wheel",
            Duration = 8,
            Effect = new SolidEffect { Color = "#FF0000" },
            Automation =
            {
                new AutomationLane
                {
                    Target = AutomationTarget.HueShift,
                    Animated = true,
                    Keys = { new Keyframe { Time = 0, Value = 0 }, new Keyframe { Time = 8, Value = 1 } },
                },
            },
        },
    ];
}
