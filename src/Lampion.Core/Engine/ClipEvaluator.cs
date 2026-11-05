using Lampion.Core.Color;
using Lampion.Core.Model.Animation;
using Lampion.Core.Model.Effects;
using Lampion.Core.Model.Timeline;

namespace Lampion.Core.Engine;

/// <summary>One light clip's state for one fixture: the effect sample with automation applied. Pure and allocation-free.</summary>
internal static class ClipEvaluator
{
    /// <summary>Overwrites <paramref name="state"/> with the clip's light for fixture <paramref name="index"/> of <paramref name="count"/>.</summary>
    /// <param name="clip">The clip.</param>
    /// <param name="time">Show time in seconds.</param>
    /// <param name="index">The fixture's place in the track order.</param>
    /// <param name="count">Number of fixtures the track drives.</param>
    /// <param name="clampToClip">
    /// For clips that are one half of a transition and run past their own edges, so the effect keeps
    /// animating rather than freezing: their fades are measured as if inside the clip.
    /// </param>
    /// <param name="state">Receives the clip's state (premultiplied light, automation roles and raw channels).</param>
    /// <returns>The clip's opacity from its fades and opacity automation, 0..1.</returns>
    public static double Evaluate(LightClip clip, double time, int index, int count, bool clampToClip, ref FixtureState state)
    {
        var local = time - clip.Start;
        var envelopeTime = clampToClip ? Math.Clamp(local, 0, clip.Duration) : local;
        var sample = clip.Effect.Sample(new EffectContext(local + clip.EffectOffset, clip.EffectSpan ?? clip.Duration, index, count));
        var color = sample.Color;
        var intensity = sample.Intensity;
        var opacity = clip.Envelope(envelopeTime);
        state = default;

        var lanes = clip.Automation;
        for (var i = 0; i < lanes.Count; i++)
        {
            var lane = lanes[i];
            if (!lane.Enabled || lane.Keys.Count == 0)
                continue;
            var v = lane.Evaluate(local);
            switch (lane.Target)
            {
                case AutomationTarget.HueShift:
                    var (h, s, val) = color.ToHsv();
                    color = Rgb.FromHsv(h + v, s, val);
                    break;
                case AutomationTarget.Saturation:
                    var hsv = color.ToHsv();
                    color = Rgb.FromHsv(hsv.H, hsv.S * Math.Max(0, v), hsv.V);
                    break;
                case AutomationTarget.Intensity:
                    intensity *= Math.Max(0, v);
                    break;
                case AutomationTarget.Opacity:
                    opacity *= Rgb.Clamp01(v);
                    break;
                case AutomationTarget.White:
                    state.White = Rgb.Clamp01(v);
                    break;
                case AutomationTarget.Red:
                    color = color with { R = Rgb.Clamp01(v) };
                    break;
                case AutomationTarget.Green:
                    color = color with { G = Rgb.Clamp01(v) };
                    break;
                case AutomationTarget.Blue:
                    color = color with { B = Rgb.Clamp01(v) };
                    break;
                case AutomationTarget.Role:
                    state.SetRole(lane.Role, Rgb.Clamp01(v));
                    break;
                case AutomationTarget.Channel:
                    state.SetRaw(lane.Channel - 1, Rgb.Clamp01(v));
                    break;
            }
        }

        state.Light = color * intensity;
        if (state.White is { } w)
            state.White = w * intensity;
        return opacity;
    }
}
