using Lampion.Core.Model;

namespace Lampion.App.Editor;

/// <summary>Timeline edits shared by the timeline's context menus, the main menu and keyboard shortcuts.</summary>
public static class TimelineOps
{
    private static string? _clipboard;

    public static Track AddLightTrack(EditorState state, IEnumerable<string>? fixtureIds = null, IEnumerable<string>? groupIds = null, int? index = null)
    {
        var count = state.Project.Tracks.Count(t => t.Kind == TrackKind.Light);
        var track = new Track
        {
            Name = $"Lights {count + 1}",
            FixtureIds = fixtureIds?.ToList() ?? [],
            GroupIds = groupIds?.ToList() ?? [],
            Order = FixtureOrder.LeftToRight,
        };
        var at = index ?? state.Project.Tracks.FindIndex(t => t.Kind != TrackKind.Light);
        state.Project.Tracks.Insert(at < 0 ? state.Project.Tracks.Count : at, track);
        state.Commit();
        state.SelectTrack(track.Id);
        return track;
    }

    public static Track EnsureMediaTrack(EditorState state, TrackKind kind)
    {
        var track = state.Project.Tracks.Find(t => t.Kind == kind);
        if (track is not null)
            return track;
        track = new Track { Name = kind.ToString(), Kind = kind };
        state.Project.Tracks.Add(track);
        return track;
    }

    public static LightClip AddEffectClip(EditorState state, Track track, string effectKind, double start, double? duration = null)
    {
        var effect = Effect.Create(effectKind);
        var clip = new LightClip
        {
            Name = effect.DisplayName,
            Start = Math.Max(0, start),
            Duration = duration ?? state.SecondsPerBeat * 8,
            Effect = effect,
        };
        track.Clips.Add(clip);
        track.SortClips();
        state.Commit();
        state.SelectClip(clip.Id);
        return clip;
    }

    public static void DeleteSelection(EditorState state)
    {
        if (state.SelectedKey is { } key)
        {
            foreach (var clip in state.Project.AllClips.OfType<LightClip>())
                foreach (var lane in clip.Automation.Where(l => l.Id == key.LaneId))
                    lane.Keys.Remove(key.Key);
            state.SelectedKey = null;
            state.Commit();
            return;
        }
        switch (state.SelectionKind)
        {
            case SelectionKind.Clip:
                foreach (var track in state.Project.Tracks)
                    track.Clips.RemoveAll(c => state.SelectedClipIds.Contains(c.Id));
                break;
            case SelectionKind.Fixture:
                state.Project.Fixtures.RemoveAll(f => state.SelectedFixtureIds.Contains(f.Id));
                foreach (var group in state.Project.Groups)
                    group.FixtureIds.RemoveAll(state.SelectedFixtureIds.Contains);
                foreach (var track in state.Project.Tracks)
                    track.FixtureIds.RemoveAll(state.SelectedFixtureIds.Contains);
                break;
            case SelectionKind.StageObject:
                state.Project.Stage.Objects.RemoveAll(o => o.Id == state.SelectedObjectId);
                break;
            case SelectionKind.Track:
                state.Project.Tracks.RemoveAll(t => t.Id == state.SelectedTrackId);
                break;
            default:
                return;
        }
        state.Commit();
        state.Deselect();
    }

    /// <summary>Premiere's razor (Ctrl+K): cuts the selected clips — or every clip — under the playhead.</summary>
    public static void SplitAtPlayhead(EditorState state)
    {
        var t = state.Playhead;
        var targets = state.Project.Tracks
            .SelectMany(track => track.Clips.Select(clip => (track, clip)))
            .Where(x => x.clip.Start < t - 0.01 && x.clip.End > t + 0.01)
            .Where(x => state.SelectedClipIds.Count == 0 || state.SelectedClipIds.Contains(x.clip.Id))
            .ToList();
        if (targets.Count == 0)
            return;
        foreach (var (track, clip) in targets)
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
                        lane.Keys = lane.Keys.Where(k => k.Time > cut).Select(k => new Keyframe { Time = k.Time - cut, Value = k.Value, Easing = k.Easing }).ToList();
                        lane.Keys.Insert(0, new Keyframe { Time = 0, Value = startValue });
                    }
                    break;
                case MediaClip when right is MediaClip m:
                    m.MediaOffset += cut;
                    break;
            }
            track.Clips.Add(right);
            track.SortClips();
        }
        state.Commit();
    }

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
        foreach (var (id, i) in added.Select((id, i) => (id, i)))
            state.SelectClip(id, additive: i > 0);
    }

    public static void Copy(EditorState state)
    {
        if (state.PrimaryClip is { } clip)
            _clipboard = System.Text.Json.JsonSerializer.Serialize<Clip>(clip, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
    }

    public static void Paste(EditorState state)
    {
        if (_clipboard is null)
            return;
        var clip = System.Text.Json.JsonSerializer.Deserialize<Clip>(_clipboard, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase })!.Clone();
        var kind = clip is LightClip ? TrackKind.Light : state.Project.Tracks.FirstOrDefault(t => t.Clips.Any(c => c is MediaClip m && m.Path == ((MediaClip)clip).Path))?.Kind ?? TrackKind.Audio;
        var track = state.SelectedTrack is { } selected && selected.Kind == kind ? selected : state.Project.Tracks.FirstOrDefault(t => t.Kind == kind);
        if (track is null)
            return;
        clip.Start = state.Playhead;
        track.Clips.Add(clip);
        track.SortClips();
        state.Commit();
        state.SelectClip(clip.Id);
    }

    public static void SetTransition(EditorState state, LightClip clip, TransitionKind? kind)
    {
        clip.TransitionIn = kind is { } k ? new Transition { Kind = k, Duration = Math.Min(clip.Duration, state.SecondsPerBeat * 2) } : null;
        state.Commit();
    }

    public static AutomationLane AddLane(EditorState state, LightClip clip, AutomationTarget target, ChannelRole role = ChannelRole.Strobe, int channel = 1)
    {
        var lane = new AutomationLane { Target = target, Role = role, Channel = channel };
        var value = lane.DefaultValue;
        if (target == AutomationTarget.Role && role is ChannelRole.Pan or ChannelRole.Tilt)
            value = 0.5;
        lane.Keys.Add(new Keyframe { Time = 0, Value = value });
        if (target == AutomationTarget.HueShift)
            lane.Keys.Add(new Keyframe { Time = clip.Duration, Value = 1 });
        else
            lane.Keys.Add(new Keyframe { Time = clip.Duration, Value = value });
        clip.Automation.Add(lane);
        state.Commit();
        return lane;
    }

    public static void AddMarker(EditorState state)
    {
        state.Project.Markers.Add(new Marker { Time = state.Playhead, Label = $"M{state.Project.Markers.Count + 1}" });
        state.Commit();
    }

    /// <summary>Clips on a track whose automation lanes the timeline shows, keyed by what they drive.</summary>
    public static List<(string Key, string Label)> LaneKeys(Track track) =>
        track.Clips.OfType<LightClip>()
            .SelectMany(c => c.Automation)
            .Select(l => (Key: LaneKey(l), l.Label))
            .DistinctBy(x => x.Key)
            .ToList();

    public static string LaneKey(AutomationLane lane) =>
        lane.Target switch
        {
            AutomationTarget.Role => $"role:{lane.Role}",
            AutomationTarget.Channel => $"ch:{lane.Channel}",
            _ => lane.Target.ToString(),
        };

}
