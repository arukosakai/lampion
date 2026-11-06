using Lampion.Core.Model.Animation;
using Lampion.Core.Model.Effects;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Timeline;

namespace Lampion.Core.Model.Showcase;

/// <summary>Builds the showcase's clips and lanes in bars and beats of a 128 bpm track instead of raw seconds.</summary>
internal static class Cue
{
    /// <summary>Tempo of the show.</summary>
    public const double Bpm = 128;

    /// <summary>One beat, seconds.</summary>
    public const double Beat = 60 / Bpm;

    /// <summary>One 4/4 bar, seconds.</summary>
    public const double Bar = 4 * Beat;

    /// <summary>Beats per second, for effects that pulse on the beat.</summary>
    public const double BeatRate = Bpm / 60;

    /// <summary><paramref name="bars"/> bars in seconds.</summary>
    public static double Bars(double bars) => bars * Bar;

    /// <summary>A light clip from bar <paramref name="start"/> lasting <paramref name="bars"/> bars; fades are in bars too.</summary>
    public static LightClip Clip(string name, double start, double bars, Effect effect, double fadeIn = 0, double fadeOut = 0, Transition? transition = null) =>
        new()
        {
            Name = name,
            Start = Bars(start),
            Duration = Bars(bars),
            Effect = effect,
            FadeIn = Bars(fadeIn),
            FadeOut = Bars(fadeOut),
            FadeEasing = Easing.EaseInOut,
            TransitionIn = transition,
        };

    /// <summary>A transition into a clip, <paramref name="bars"/> long and centred on the cut.</summary>
    public static Transition Into(TransitionKind kind, double bars) => new() { Kind = kind, Duration = Bars(bars) };

    /// <summary>A lane with keys at clip-local bar positions.</summary>
    public static AutomationLane Lane(AutomationTarget target, params (double Bar, double Value, Easing Easing)[] keys) => Keyed(new AutomationLane { Target = target }, keys);

    /// <summary>A lane writing <paramref name="role"/>'s DMX channel, with keys at clip-local bar positions.</summary>
    public static AutomationLane Lane(ChannelRole role, params (double Bar, double Value, Easing Easing)[] keys) =>
        Keyed(new AutomationLane { Target = AutomationTarget.Role, Role = role }, keys);

    /// <summary>
    /// A smooth back-and-forth on <paramref name="role"/> around <paramref name="centre"/>, peaking every half
    /// <paramref name="period"/> bars. <paramref name="late"/> starts it a quarter period behind, so a pan swing and
    /// a late tilt swing trace a circle; a negative <paramref name="amplitude"/> mirrors it.
    /// </summary>
    public static AutomationLane Swing(ChannelRole role, double bars, double period, double centre, double amplitude, bool late = false)
    {
        var lane = new AutomationLane { Target = AutomationTarget.Role, Role = role };
        var t = 0.0;
        var sign = 1;
        if (late)
        {
            lane.Keys.Add(new Keyframe { Time = 0, Value = centre, Easing = Easing.EaseOut });
            t = period / 4;
        }
        for (; t <= bars + 1e-9; t += period / 2, sign = -sign)
            lane.Keys.Add(new Keyframe { Time = Bars(t), Value = centre + sign * amplitude, Easing = Easing.EaseInOut });
        return lane;
    }

    /// <summary><paramref name="role"/> snapping between <paramref name="a"/> and <paramref name="b"/> on every <paramref name="step"/> bars, easing out like a mover hitting its mark.</summary>
    public static AutomationLane Steps(ChannelRole role, double bars, double step, double a, double b)
    {
        var lane = new AutomationLane { Target = AutomationTarget.Role, Role = role };
        var count = (int)Math.Round(bars / step);
        for (var i = 0; i <= count; i++)
            lane.Keys.Add(new Keyframe { Time = Bars(i * step), Value = i % 2 == 0 ? a : b, Easing = Easing.EaseOut });
        return lane;
    }

    /// <summary>The same clip with <paramref name="lanes"/> added, so cues read as one expression.</summary>
    public static LightClip With(this LightClip clip, params AutomationLane[] lanes)
    {
        clip.Automation.AddRange(lanes);
        return clip;
    }

    private static AutomationLane Keyed(AutomationLane lane, (double Bar, double Value, Easing Easing)[] keys)
    {
        foreach (var (bar, value, easing) in keys)
            lane.Keys.Add(new Keyframe { Time = Bars(bar), Value = value, Easing = easing });
        return lane;
    }
}
