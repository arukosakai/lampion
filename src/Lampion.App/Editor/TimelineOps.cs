using Lampion.Core.Model.Animation;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Timeline;
using Lampion.Core.Persistence;

namespace Lampion.App.Editor;

/// <summary>
/// One place to reach every timeline edit from menus, shortcuts and panels. Each member forwards to the
/// cohesive edit class that implements it (<see cref="TrackEdits"/>, <see cref="ClipEdits"/>,
/// <see cref="AutomationEdits"/>, <see cref="ClipClipboard"/>, <see cref="MarkerEdits"/>,
/// <see cref="SelectionEdits"/>); see those for details. Every edit commits an undo step. UI thread only.
/// </summary>
internal static class TimelineOps
{
    /// <inheritdoc cref="TrackEdits.AddLightTrack"/>
    public static Track AddLightTrack(EditorState state, IEnumerable<string>? fixtureIds = null, IEnumerable<string>? groupIds = null, int? index = null) =>
        TrackEdits.AddLightTrack(state, fixtureIds, groupIds, index);

    /// <inheritdoc cref="TrackEdits.EnsureMediaTrack"/>
    public static Track EnsureMediaTrack(EditorState state, TrackKind kind) => TrackEdits.EnsureMediaTrack(state, kind);

    /// <inheritdoc cref="ClipEdits.AddEffectClip"/>
    public static LightClip AddEffectClip(EditorState state, Track track, string effectKind, double start, double? duration = null) =>
        ClipEdits.AddEffectClip(state, track, effectKind, start, duration);

    /// <inheritdoc cref="ClipEdits.AddPresetClip"/>
    public static LightClip AddPresetClip(EditorState state, Track track, EffectPreset preset, double start) =>
        ClipEdits.AddPresetClip(state, track, preset, start);

    /// <inheritdoc cref="ClipEdits.ApplyPreset"/>
    public static void ApplyPreset(EditorState state, LightClip clip, EffectPreset preset) => ClipEdits.ApplyPreset(state, clip, preset);

    /// <inheritdoc cref="SelectionEdits.DeleteSelection"/>
    public static void DeleteSelection(EditorState state) => SelectionEdits.DeleteSelection(state);

    /// <inheritdoc cref="ClipEdits.SplitAtPlayhead"/>
    public static void SplitAtPlayhead(EditorState state) => ClipEdits.SplitAtPlayhead(state);

    /// <inheritdoc cref="ClipEdits.Duplicate"/>
    public static void Duplicate(EditorState state) => ClipEdits.Duplicate(state);

    /// <inheritdoc cref="ClipClipboard.Copy"/>
    public static void Copy(EditorState state) => ClipClipboard.Copy(state);

    /// <inheritdoc cref="ClipClipboard.Paste"/>
    public static void Paste(EditorState state) => ClipClipboard.Paste(state);

    /// <inheritdoc cref="ClipEdits.SetTransition"/>
    public static void SetTransition(EditorState state, LightClip clip, TransitionKind? kind) => ClipEdits.SetTransition(state, clip, kind);

    /// <inheritdoc cref="AutomationEdits.AddLane"/>
    public static AutomationLane AddLane(EditorState state, LightClip clip, AutomationTarget target, ChannelRole role = ChannelRole.Strobe, int channel = 1) =>
        AutomationEdits.AddLane(state, clip, target, role, channel);

    /// <inheritdoc cref="MarkerEdits.AddMarker"/>
    public static void AddMarker(EditorState state) => MarkerEdits.AddMarker(state);

    /// <summary>
    /// The keyframe rows an expanded track shows: one per distinct thing its clips animate, in first-seen
    /// order, as (<see cref="LaneKey"/>, label). Allocates; the timeline itself uses its cached layout.
    /// </summary>
    public static List<(string Key, string Label)> LaneKeys(Track track) =>
        track.Clips.OfType<LightClip>()
            .SelectMany(c => c.Automation.Where(l => l.Animated))
            .Select(l => (Key: LaneKey(l), l.Label))
            .DistinctBy(x => x.Key)
            .ToList();

    /// <summary>A string naming what <paramref name="lane"/> drives, e.g. "role:Pan", "ch:3" or "Intensity". Allocates.</summary>
    public static string LaneKey(AutomationLane lane) =>
        lane.Target switch
        {
            AutomationTarget.Role => $"role:{lane.Role}",
            AutomationTarget.Channel => $"ch:{lane.Channel}",
            _ => lane.Target.ToString(),
        };
}
