using Lampion.Core.Color;
using Lampion.Core.Model.Animation;
using Lampion.Core.Model.Timeline;

namespace Lampion.Core.Engine;

/// <summary>A light clip's contribution including its transition in, where one is running. Pure and allocation-free.</summary>
internal static class TransitionEvaluator
{
    private const double CutTolerance = 1e-3;
    private const double WipeEdge = 0.25;

    /// <summary>Evaluates <paramref name="clip"/> at <paramref name="time"/>, crossing over from the clip that ends at its start while its transition runs.</summary>
    /// <param name="clip">The clip.</param>
    /// <param name="clips">The track's clips, searched for the outgoing clip.</param>
    /// <param name="time">Show time in seconds.</param>
    /// <param name="index">The fixture's place in the track order.</param>
    /// <param name="count">Number of fixtures the track drives.</param>
    /// <param name="state">Receives the state when the clip contributes.</param>
    /// <param name="alpha">The contribution's coverage, 0..1.</param>
    /// <returns>False when the clip (and its transition) is not active at <paramref name="time"/>.</returns>
    public static bool TryEvaluate(LightClip clip, List<Clip> clips, double time, int index, int count, ref FixtureState state, out double alpha)
    {
        var transition = clip.TransitionIn;
        var half = transition is null ? 0 : Math.Max(0, transition.Duration) / 2;
        if (transition is null || half <= 0 || time < clip.Start - half || time >= clip.Start + half)
        {
            if (time >= clip.Start && time < clip.End)
            {
                alpha = ClipEvaluator.Evaluate(clip, time, index, count, clampToClip: false, ref state);
                return true;
            }
            alpha = 0;
            return false;
        }

        // Inside the transition window: this clip takes over from the clip that ends at its start.
        var outgoing = Outgoing(clip, clips);
        var progress = EasingCurve.Apply(transition.Easing, (time - (clip.Start - half)) / (2 * half));
        FixtureState incoming = default;
        var incomingAlpha = ClipEvaluator.Evaluate(clip, time, index, count, clampToClip: true, ref incoming);
        var previousAlpha = 0.0;
        if (outgoing is null)
            state = default;
        else
            previousAlpha = ClipEvaluator.Evaluate(outgoing, time, index, count, clampToClip: true, ref state);

        switch (transition.Kind)
        {
            case TransitionKind.DipToBlack:
                if (progress < 0.5)
                {
                    state.Scale(1 - progress * 2);
                    alpha = previousAlpha;
                }
                else
                {
                    state = incoming;
                    state.Scale(progress * 2 - 1);
                    alpha = incomingAlpha;
                }
                return true;
            case TransitionKind.Wipe:
                var spread = count <= 1 ? 0.5 : index / (double)(count - 1);
                progress = Rgb.Clamp01((progress * (1 + WipeEdge) - spread) / WipeEdge);
                break;
        }
        state.Blend(in incoming, progress, BlendMode.Normal);
        alpha = previousAlpha + (incomingAlpha - previousAlpha) * progress;
        return true;
    }

    /// <summary>The first other light clip ending at <paramref name="clip"/>'s start, or null.</summary>
    private static LightClip? Outgoing(LightClip clip, List<Clip> clips)
    {
        for (var i = 0; i < clips.Count; i++)
            if (clips[i] is LightClip candidate && candidate != clip && Math.Abs(candidate.End - clip.Start) < CutTolerance)
                return candidate;
        return null;
    }
}
