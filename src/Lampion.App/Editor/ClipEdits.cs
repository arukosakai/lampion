using Lampion.Core.Model.Animation;
using Lampion.Core.Model.Effects;
using Lampion.Core.Model.Timeline;
using Lampion.Core.Persistence;

namespace Lampion.App.Editor;

/// <summary>Undoable edits to clips: adding them, applying presets, splitting, duplicating and transitions.</summary>
internal static class ClipEdits
{
    // Splits closer than this to a clip edge would leave a sliver, so the razor skips that clip.
    private const double SplitMargin = 0.01;

    /// <summary>Adds a clip playing a new <paramref name="effectKind"/> effect, commits and selects it.</summary>
    /// <param name="state">The editor.</param>
    /// <param name="track">A light track.</param>
    /// <param name="effectKind">An <see cref="EffectCatalog"/> kind; unknown kinds give a solid colour.</param>
    /// <param name="start">Seconds; negative values start at 0.</param>
    /// <param name="duration">Seconds, or null for eight beats.</param>
    /// <returns>The new clip.</returns>
    public static LightClip AddEffectClip(EditorState state, Track track, string effectKind, double start, double? duration = null)
    {
        var effect = EffectCatalog.Create(effectKind);
        var clip = new LightClip
        {
            Name = effect.DisplayName,
            Start = Math.Max(0, start),
            Duration = duration ?? state.SecondsPerBeat * 8,
            Effect = effect,
        };
        return Place(state, track, clip);
    }

    /// <summary>Adds a clip made from <paramref name="preset"/> at <paramref name="start"/> seconds, commits and selects it.</summary>
    public static LightClip AddPresetClip(EditorState state, Track track, EffectPreset preset, double start) =>
        Place(state, track, preset.CreateClip(start));

    /// <summary>Replaces the clip's look with <paramref name="preset"/> (and takes its name), commits and selects it.</summary>
    public static void ApplyPreset(EditorState state, LightClip clip, EffectPreset preset)
    {
        preset.ApplyTo(clip);
        clip.Name = preset.Name;
        state.Commit();
        state.SelectClip(clip.Id);
    }

    /// <summary>
    /// The razor (Ctrl+K): cuts the selected clips, or every clip when none is selected, under the playhead.
    /// Both halves keep playing exactly what the whole clip did. Commits once; does nothing if nothing is cut.
    /// </summary>
    public static void SplitAtPlayhead(EditorState state)
    {
        var t = state.Playhead;
        var targets = state.Project.Tracks
            .SelectMany(track => track.Clips.Select(clip => (track, clip)))
            .Where(x => x.clip.Start < t - SplitMargin && x.clip.End > t + SplitMargin)
            .Where(x => state.SelectedClipIds.Count == 0 || state.SelectedClipIds.Contains(x.clip.Id))
            .ToList();
        if (targets.Count == 0)
            return;
        foreach (var (track, clip) in targets)
        {
            track.Clips.Add(Split(clip, t));
            track.SortClips();
        }
        state.Commit();
    }

    /// <summary>Places a copy of each selected clip right after it, commits and selects the copies.</summary>
    public static void Duplicate(EditorState state)
    {
        var added = new List<string>();
        foreach (var track in state.Project.Tracks)
            foreach (var clip in track.Clips.Where(c => state.SelectedClipIds.Contains(c.Id)).ToList())
            {
                var copy = clip.Clone();
                copy.Start = clip.End;
                track.Clips.Add(copy);
                track.SortClips();
                added.Add(copy.Id);
            }
        if (added.Count == 0)
            return;
        state.Commit();
        for (var i = 0; i < added.Count; i++)
            state.SelectClip(added[i], additive: i > 0);
    }

    /// <summary>Gives the clip a transition in of <paramref name="kind"/> (two beats, at most the clip's length), or a hard cut for null, and commits.</summary>
    public static void SetTransition(EditorState state, LightClip clip, TransitionKind? kind)
    {
        clip.TransitionIn = kind is { } k ? new Transition { Kind = k, Duration = Math.Min(clip.Duration, state.SecondsPerBeat * 2) } : null;
        state.Commit();
    }

    private static LightClip Place(EditorState state, Track track, LightClip clip)
    {
        track.Clips.Add(clip);
        track.SortClips();
        state.Commit();
        state.SelectClip(clip.Id);
        return clip;
    }

    /// <summary>Shortens <paramref name="clip"/> to end at <paramref name="t"/> and returns the right half.</summary>
    private static Clip Split(Clip clip, double t)
    {
        var right = clip.Clone();
        right.Start = t;
        right.Duration = clip.End - t;
        var cut = t - clip.Start;
        clip.Duration = cut;
        switch (clip)
        {
            case LightClip left when right is LightClip r:
                // The left half keeps the fade-in and transition, the right half the fade-out; automation
                // keys are rebased so both halves keep playing exactly what the whole clip did.
                left.FadeOut = 0;
                r.FadeIn = 0;
                r.TransitionIn = null;
                left.EffectSpan = r.EffectSpan = left.EffectSpan ?? cut + right.Duration;
                r.EffectOffset = left.EffectOffset + cut;
                foreach (var lane in r.Automation)
                {
                    var startValue = left.Automation.First(l => l.Target == lane.Target && l.Role == lane.Role && l.Channel == lane.Channel).Evaluate(cut);
                    var kept = lane.Keys.Where(k => k.Time > cut).Select(k => new Keyframe { Time = k.Time - cut, Value = k.Value, Easing = k.Easing }).ToList();
                    lane.Keys.Clear();
                    lane.Keys.AddRange(kept);
                    lane.Keys.Insert(0, new Keyframe { Time = 0, Value = startValue });
                }
                break;
            case MediaClip when right is MediaClip m:
                m.MediaOffset += cut;
                break;
        }
        return right;
    }
}
