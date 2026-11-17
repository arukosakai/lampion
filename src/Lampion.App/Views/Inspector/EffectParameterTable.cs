using Lampion.App.Controls;
using Lampion.Core.Model.Animation;
using Lampion.Core.Model.Effects;
using LightEffect = Lampion.Core.Model.Effects.Effect;

namespace Lampion.App.Views.Inspector;

/// <summary>
/// The parameters Effect Controls shows for each stock effect, in display order (the effect's own
/// properties first, <see cref="LightEffect.Intensity"/> last). A new effect in Core needs a row here;
/// until it has one, it shows no parameters.
/// </summary>
internal static class EffectParameterTable
{
    private static readonly ScrubRange Percent = ScrubRange.Percent;
    private static readonly ScrubRange Spread = new(0, 4, 0.01, Unit: "×");
    private static readonly ScrubRange Hue = new(0, 1, 1 / 360.0, 360, Unit: "°");
    private static readonly ScrubRange Seconds = new(0, 8, 0.005, Unit: " s");
    private static readonly ScrubRange Tail = new(0, 12, 0.02, Unit: " fx");
    private static readonly ScrubRange Wavelength = new(0.25, 32, 0.02, Unit: " fx");
    private static readonly ScrubRange Repeat = new(0, 8, 0.01, Unit: "×");
    private static readonly ScrubRange Phase = new(-1, 1, 0.005);

    private static readonly EffectParameter Intensity = new NumberParameter<LightEffect>("Intensity", Percent, e => e.Intensity, (e, v) => e.Intensity = v);

    private static readonly EffectParameter[] Solid = [Color<SolidEffect>("Color", e => e.Color, (e, v) => e.Color = v), Intensity];

    private static readonly EffectParameter[] Fade =
    [
        Color<ColorFadeEffect>("From", e => e.From, (e, v) => e.From = v),
        Color<ColorFadeEffect>("To", e => e.To, (e, v) => e.To = v),
        Number<ColorFadeEffect>("Offset", Seconds, e => e.Offset, (e, v) => e.Offset = v, "offset"),
        Choice<ColorFadeEffect, Easing>("Easing", e => e.Easing, (e, v) => e.Easing = v),
        Intensity,
    ];

    private static readonly EffectParameter[] Rainbow =
    [
        Tempo<RainbowEffect>("Speed", TempoScale.Speed, e => e.Speed, (e, v) => e.Speed = v, "speed"),
        Number<RainbowEffect>("Spread", Spread, e => e.Spread, (e, v) => e.Spread = v, "spread"),
        Number<RainbowEffect>("Saturation", Percent, e => e.Saturation, (e, v) => e.Saturation = v, "saturation"),
        Number<RainbowEffect>("Start hue", Hue, e => e.StartHue, (e, v) => e.StartHue = v, "startHue"),
        Intensity,
    ];

    private static readonly EffectParameter[] Cycle =
    [
        new PaletteParameter<ColorCycleEffect>("Colors", e => e.Colors),
        Tempo<ColorCycleEffect>("Step time", TempoScale.StepTime, e => e.StepTime, (e, v) => e.StepTime = v, "stepTime"),
        Number<ColorCycleEffect>("Fade ratio", Percent, e => e.FadeRatio, (e, v) => e.FadeRatio = v, "fadeRatio"),
        Number<ColorCycleEffect>("Fixture offset", Phase, e => e.FixtureOffset, (e, v) => e.FixtureOffset = v, "fixtureOffset"),
        Intensity,
    ];

    private static readonly EffectParameter[] Chase =
    [
        new PaletteParameter<ChaseEffect>("Colors", e => e.Colors),
        Tempo<ChaseEffect>("Step time", TempoScale.StepTime, e => e.StepTime, (e, v) => e.StepTime = v, "stepTime"),
        Number<ChaseEffect>("Tail", Tail, e => e.Tail, (e, v) => e.Tail = v, "tail"),
        Choice<ChaseEffect, ChaseDirection>("Direction", e => e.Direction, (e, v) => e.Direction = v),
        Number<ChaseEffect>("Background", Percent, e => e.Background, (e, v) => e.Background = v, "background"),
        Intensity,
    ];

    private static readonly EffectParameter[] Strobe =
    [
        Color<StrobeEffect>("Color", e => e.Color, (e, v) => e.Color = v),
        Tempo<StrobeEffect>("Rate", TempoScale.Rate, e => e.Rate, (e, v) => e.Rate = v, "rate"),
        Number<StrobeEffect>("Duty", Percent, e => e.Duty, (e, v) => e.Duty = v, "duty"),
        Intensity,
    ];

    private static readonly EffectParameter[] Pulse =
    [
        Color<PulseEffect>("Color", e => e.Color, (e, v) => e.Color = v),
        Tempo<PulseEffect>("Rate", TempoScale.Rate, e => e.Rate, (e, v) => e.Rate = v, "rate"),
        Number<PulseEffect>("Minimum", Percent, e => e.Minimum, (e, v) => e.Minimum = v, "minimum"),
        Choice<PulseEffect, Waveform>("Wave", e => e.Wave, (e, v) => e.Wave = v),
        Number<PulseEffect>("Phase", Phase, e => e.Phase, (e, v) => e.Phase = v, "phase"),
        Intensity,
    ];

    private static readonly EffectParameter[] Sparkle =
    [
        Color<SparkleEffect>("Color", e => e.Color, (e, v) => e.Color = v),
        Tempo<SparkleEffect>("Rate", TempoScale.Rate, e => e.Rate, (e, v) => e.Rate = v, "rate"),
        Number<SparkleEffect>("Decay", Seconds, e => e.Decay, (e, v) => e.Decay = v, "decay"),
        new IntegerParameter<SparkleEffect>("Seed", e => e.Seed, (e, v) => e.Seed = v),
        Intensity,
    ];

    private static readonly EffectParameter[] EvenSparkle =
    [
        Color<EvenSparkleEffect>("Color", e => e.Color, (e, v) => e.Color = v),
        Tempo<EvenSparkleEffect>("Rate", TempoScale.Rate, e => e.Rate, (e, v) => e.Rate = v, "rate"),
        Number<EvenSparkleEffect>("Decay", Seconds, e => e.Decay, (e, v) => e.Decay = v, "decay"),
        Choice<EvenSparkleEffect, SparkleOrder>("Order", e => e.Order, (e, v) => e.Order = v),
        new IntegerParameter<EvenSparkleEffect>("Seed", e => e.Seed, (e, v) => e.Seed = v),
        Intensity,
    ];

    private static readonly EffectParameter[] Gradient =
    [
        new PaletteParameter<GradientEffect>("Colors", e => e.Colors),
        Number<GradientEffect>("Repeat", Repeat, e => e.Repeat, (e, v) => e.Repeat = v, "repeat"),
        Tempo<GradientEffect>("Speed", TempoScale.Speed, e => e.Speed, (e, v) => e.Speed = v, "speed"),
        Intensity,
    ];

    private static readonly EffectParameter[] Wave =
    [
        Color<ColorWaveEffect>("Color", e => e.Color, (e, v) => e.Color = v),
        Color<ColorWaveEffect>("Base", e => e.Base, (e, v) => e.Base = v),
        Tempo<ColorWaveEffect>("Rate", TempoScale.Rate, e => e.Rate, (e, v) => e.Rate = v, "rate"),
        Number<ColorWaveEffect>("Wavelength", Wavelength, e => e.Wavelength, (e, v) => e.Wavelength = v, "wavelength"),
        Choice<ColorWaveEffect, Waveform>("Wave", e => e.Wave, (e, v) => e.Wave = v),
        Intensity,
    ];

    private static readonly EffectParameter[] Fire =
    [
        Color<FireEffect>("Ember", e => e.Ember, (e, v) => e.Ember = v),
        Color<FireEffect>("Flame", e => e.Flame, (e, v) => e.Flame = v),
        Tempo<FireEffect>("Rate", TempoScale.Rate, e => e.Rate, (e, v) => e.Rate = v, "rate"),
        Number<FireEffect>("Minimum", Percent, e => e.Minimum, (e, v) => e.Minimum = v, "minimum"),
        new IntegerParameter<FireEffect>("Seed", e => e.Seed, (e, v) => e.Seed = v),
        Intensity,
    ];

    private static readonly EffectParameter[] Lightning =
    [
        Color<LightningEffect>("Color", e => e.Color, (e, v) => e.Color = v),
        Tempo<LightningEffect>("Rate", TempoScale.Rate, e => e.Rate, (e, v) => e.Rate = v, "rate"),
        Number<LightningEffect>("Decay", Seconds, e => e.Decay, (e, v) => e.Decay = v, "decay"),
        Choice<LightningEffect, LightningMode>("Mode", e => e.Mode, (e, v) => e.Mode = v),
        new IntegerParameter<LightningEffect>("Seed", e => e.Seed, (e, v) => e.Seed = v),
        Intensity,
    ];

    private static readonly EffectParameter[] Heartbeat =
    [
        Color<HeartbeatEffect>("Color", e => e.Color, (e, v) => e.Color = v),
        Tempo<HeartbeatEffect>("Rate", TempoScale.Rate, e => e.Rate, (e, v) => e.Rate = v, "rate"),
        Number<HeartbeatEffect>("Minimum", Percent, e => e.Minimum, (e, v) => e.Minimum = v, "minimum"),
        Number<HeartbeatEffect>("Phase", Phase, e => e.Phase, (e, v) => e.Phase = v, "phase"),
        Intensity,
    ];

    private static readonly EffectParameter[] Hit =
    [
        Color<HitEffect>("Color", e => e.Color, (e, v) => e.Color = v),
        Number<HitEffect>("Decay", Seconds, e => e.Decay, (e, v) => e.Decay = v, "decay"),
        Tempo<HitEffect>("Interval", TempoScale.Interval, e => e.Interval, (e, v) => e.Interval = v, "interval"),
        Number<HitEffect>("Offset", Seconds, e => e.Offset, (e, v) => e.Offset = v, "offset"),
        Intensity,
    ];

    /// <summary>The parameters to show for <paramref name="effect"/>; empty for an effect type without a row here.</summary>
    public static IReadOnlyList<EffectParameter> For(LightEffect effect) =>
        effect switch
        {
            SolidEffect => Solid,
            ColorFadeEffect => Fade,
            RainbowEffect => Rainbow,
            ColorCycleEffect => Cycle,
            ChaseEffect => Chase,
            StrobeEffect => Strobe,
            PulseEffect => Pulse,
            SparkleEffect => Sparkle,
            EvenSparkleEffect => EvenSparkle,
            GradientEffect => Gradient,
            ColorWaveEffect => Wave,
            FireEffect => Fire,
            LightningEffect => Lightning,
            HeartbeatEffect => Heartbeat,
            HitEffect => Hit,
            _ => [],
        };

    private static NumberParameter<T> Number<T>(string label, ScrubRange range, Func<T, double> get, Action<T, double> set, string key)
        where T : LightEffect => new(label, range, get, set) { AnimationKey = key };

    private static TempoParameter<T> Tempo<T>(string label, TempoScale scale, Func<T, double> get, Action<T, double> set, string key)
        where T : LightEffect => new(label, scale, get, set) { AnimationKey = key };

    private static ColorParameter<T> Color<T>(string label, Func<T, string> get, Action<T, string> set)
        where T : LightEffect => new(label, get, set);

    private static ChoiceParameter<T, TEnum> Choice<T, TEnum>(string label, Func<T, TEnum> get, Action<T, TEnum> set)
        where T : LightEffect
        where TEnum : struct, Enum => new(label, get, set);
}
