using Lampion.Core.Model.Effects;
using Lampion.Core.Model.Timeline;

namespace Lampion.Core.Model.Animation;

/// <summary>
/// Applies a clip's <see cref="AutomationTarget.EffectParameter"/> lanes, giving the context its effect is sampled
/// with. Pure and allocation-free.
/// </summary>
/// <remarks>
/// A keyframed speed can't simply replace the effect's property: rate × time would jump every time the rate
/// changes. Instead the speed is integrated into a phase, and the effect gets its current speed plus a warped
/// clock (phase / speed), so speed × clock is the integrated phase and the clock still runs at about real seconds
/// for decays. A lane holding one value plays exactly what setting the property would.
/// </remarks>
internal static class EffectParameterAutomation
{
    // Simpson panels per keyframe segment: exact for the eased curves of a frequency, close for a period's 1/x.
    private const int Panels = 16;

    // Below this speed the clock can't be divided out of the phase, so the phase itself is used with a speed of 1.
    private const double Stopped = 1e-9;

    /// <summary>The context for sampling <paramref name="clip"/>'s effect at <paramref name="local"/> seconds into the clip.</summary>
    public static EffectContext Context(LightClip clip, double local, int index, int count)
    {
        var time = local + clip.EffectOffset;
        var span = clip.EffectSpan ?? clip.Duration;
        var effect = clip.Effect;
        var lanes = clip.Automation;
        EffectParameterValues values = default;
        for (var i = 0; i < lanes.Count; i++)
        {
            var lane = lanes[i];
            if (lane.Target != AutomationTarget.EffectParameter || !lane.Enabled || lane.Keys.Count == 0)
                continue;
            var slot = effect.SlotOf(lane.Parameter);
            if (slot < 0)
                continue;
            var parameter = effect.AnimatableParameters[slot];
            var value = Math.Max(parameter.Minimum, lane.Evaluate(local));
            switch (parameter.Timing)
            {
                case ParameterTiming.Frequency:
                    var phase = clip.EffectOffset * Speed(parameter, lane.Evaluate(0)) + Integral(lane, parameter, local);
                    if (Math.Abs(value) < Stopped)
                    {
                        time = phase;
                        value = 1;
                    }
                    else
                        time = phase / value;
                    break;
                case ParameterTiming.Period:
                    time = (clip.EffectOffset * Speed(parameter, lane.Evaluate(0)) + Integral(lane, parameter, local)) * value;
                    break;
            }
            values.Set(slot, value);
        }
        return new EffectContext(time, span, index, count, in values);
    }

    /// <summary>Cycles per second for a keyframed value of a frequency or period parameter.</summary>
    private static double Speed(AnimatableParameter parameter, double value)
    {
        var clamped = Math.Max(parameter.Minimum, value);
        return parameter.Timing == ParameterTiming.Period ? 1 / clamped : clamped;
    }

    /// <summary>The lane's speed integrated from 0 to <paramref name="to"/> seconds into the clip; negative before 0.</summary>
    private static double Integral(AutomationLane lane, AnimatableParameter parameter, double to) =>
        Antiderivative(lane, parameter, to) - Antiderivative(lane, parameter, 0);

    // Integral from the first key to x; the value holds before the first key and after the last.
    private static double Antiderivative(AutomationLane lane, AnimatableParameter parameter, double x)
    {
        var keys = lane.Keys;
        var first = keys[0];
        if (!lane.Animated || x <= first.Time)
            return (x - first.Time) * Speed(parameter, first.Value);
        var sum = 0.0;
        for (var i = 0; i < keys.Count - 1; i++)
        {
            var a = keys[i];
            var b = keys[i + 1];
            sum += Segment(a, b, parameter, Math.Min(x, b.Time));
            if (x <= b.Time)
                return sum;
        }
        var last = keys[^1];
        return sum + (x - last.Time) * Speed(parameter, last.Value);
    }

    // Simpson's rule over a.Time..end, evaluating the segment's eased curve directly rather than searching the keys.
    private static double Segment(Keyframe a, Keyframe b, AnimatableParameter parameter, double end)
    {
        var width = b.Time - a.Time;
        if (width <= 1e-9 || end <= a.Time)
            return 0;
        var h = (end - a.Time) / Panels;
        var sum = 0.0;
        for (var k = 0; k <= Panels; k++)
        {
            var u = (k * h) / width;
            var v = a.Value + (b.Value - a.Value) * EasingCurve.Apply(a.Easing, u);
            var weight = k == 0 || k == Panels ? 1 : k % 2 == 1 ? 4 : 2;
            sum += weight * Speed(parameter, v);
        }
        return sum * h / 3;
    }
}
