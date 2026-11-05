using Lampion.Core.Color;
using Lampion.Core.Model;

namespace Lampion.Core.Engine;

/// <summary>
/// Turns the timeline into per-fixture states at a given time. Pure: the same project and time always
/// give the same result, which is what lets the preview, Art-Net output and QLC+ export agree.
/// </summary>
public sealed class ShowEvaluator(Project project)
{
    private const double CutTolerance = 1e-3;

    public Project Project { get; } = project;

    public Dictionary<string, FixtureState> Evaluate(double time)
    {
        var states = new Dictionary<string, FixtureState>(Project.Fixtures.Count);
        foreach (var fixture in Project.Fixtures)
            states[fixture.Id] = new FixtureState();

        var anySolo = Project.Tracks.Any(t => t.Kind == TrackKind.Light && t.Solo);
        // The topmost track in the list is drawn on top, as in Premiere / After Effects.
        for (var i = Project.Tracks.Count - 1; i >= 0; i--)
        {
            var track = Project.Tracks[i];
            if (track.Kind != TrackKind.Light || track.Muted || (anySolo && !track.Solo))
                continue;
            var targets = Project.ResolveTargets(track);
            if (targets.Count == 0)
                continue;
            for (var index = 0; index < targets.Count; index++)
            {
                var layer = EvaluateTrack(track, time, index, targets);
                if (layer.Alpha > 0)
                    states[targets[index].Id].Blend(layer.State, layer.Alpha, track.Blend);
            }
        }
        return states;
    }

    /// <summary>One fixture's contribution from one track, compositing overlapping clips and transitions.</summary>
    internal Layer EvaluateTrack(Track track, double time, int index, List<Fixture> targets)
    {
        var accumulated = new FixtureState();
        var alpha = 0.0;
        var clips = track.Clips;
        for (var c = 0; c < clips.Count; c++)
        {
            if (clips[c] is not LightClip clip)
                continue;
            var layer = EvaluateWithTransition(clip, clips, time, index, targets.Count);
            if (layer is not { } l || l.Alpha <= 0)
                continue;
            // Porter-Duff "over" on premultiplied light, so a half-faded clip on an empty track is half as bright.
            var outAlpha = l.Alpha + alpha * (1 - l.Alpha);
            var below = accumulated;
            below.Scale(alpha * (1 - l.Alpha));
            var top = l.State.Copy();
            top.Scale(l.Alpha);
            below.Blend(top, 1, BlendMode.Add);
            below.Scale(outAlpha > 0 ? 1 / outAlpha : 0);
            accumulated = below;
            alpha = outAlpha;
        }
        return new Layer(accumulated, alpha);
    }

    private Layer? EvaluateWithTransition(LightClip clip, List<Clip> clips, double time, int index, int count)
    {
        var transition = clip.TransitionIn;
        var half = transition is null ? 0 : Math.Max(0, transition.Duration) / 2;
        if (transition is null || half <= 0 || time < clip.Start - half || time >= clip.Start + half)
            return time >= clip.Start && time < clip.End ? EvaluateClip(clip, time, index, count, clampToClip: false) : null;

        // Inside the transition window: this clip takes over from the clip that ends at its start.
        var outgoing = clips.OfType<LightClip>().FirstOrDefault(c => c != clip && Math.Abs(c.End - clip.Start) < CutTolerance);
        var progress = AutomationLane.Ease(transition.Easing, (time - (clip.Start - half)) / (2 * half));
        var incoming = EvaluateClip(clip, time, index, count, clampToClip: true);
        var previous = outgoing is null ? new Layer(new FixtureState(), 0) : EvaluateClip(outgoing, time, index, count, clampToClip: true);

        switch (transition.Kind)
        {
            case TransitionKind.DipToBlack:
                return progress < 0.5
                    ? previous with { State = Scaled(previous.State, 1 - progress * 2) }
                    : incoming with { State = Scaled(incoming.State, progress * 2 - 1) };
            case TransitionKind.Wipe:
                const double edge = 0.25;
                var spread = count <= 1 ? 0.5 : index / (double)(count - 1);
                progress = Rgb.Clamp01((progress * (1 + edge) - spread) / edge);
                break;
        }
        var state = previous.State.Copy();
        state.Blend(incoming.State, progress, BlendMode.Normal);
        return new Layer(state, previous.Alpha + (incoming.Alpha - previous.Alpha) * progress);
    }

    // Clips this one is the outgoing/incoming half of a transition run past their own edges, so the
    // effect keeps animating rather than freezing; their fades are measured as if inside the clip.
    private static Layer EvaluateClip(LightClip clip, double time, int index, int count, bool clampToClip)
    {
        var local = time - clip.Start;
        var envelopeTime = clampToClip ? Math.Clamp(local, 0, clip.Duration) : local;
        var sample = clip.Effect.Sample(new EffectContext(local + clip.EffectOffset, clip.EffectSpan ?? clip.Duration, index, count));
        var color = sample.Color;
        var intensity = sample.Intensity;
        var opacity = clip.Envelope(envelopeTime);
        var state = new FixtureState();

        foreach (var lane in clip.Automation)
        {
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
        return new Layer(state, opacity);
    }

    private static FixtureState Scaled(FixtureState state, double k)
    {
        var copy = state.Copy();
        copy.Scale(k);
        return copy;
    }
}
