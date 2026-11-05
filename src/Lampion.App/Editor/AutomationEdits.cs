using Lampion.Core.Model.Animation;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Editor;

/// <summary>Undoable edits to automation lanes and their keyframes.</summary>
internal static class AutomationEdits
{
    /// <summary>
    /// Adds a lane driving <paramref name="target"/> with a key at each end of the clip: a full hue cycle for
    /// hue shift, centred for pan/tilt, otherwise the target's neutral value. Commits.
    /// </summary>
    /// <param name="state">The editor.</param>
    /// <param name="clip">The clip to animate.</param>
    /// <param name="target">What the lane drives.</param>
    /// <param name="role">The channel role, for <see cref="AutomationTarget.Role"/>.</param>
    /// <param name="channel">The 1-based channel, for <see cref="AutomationTarget.Channel"/>.</param>
    /// <returns>The new lane.</returns>
    public static AutomationLane AddLane(EditorState state, LightClip clip, AutomationTarget target, ChannelRole role = ChannelRole.Strobe, int channel = 1)
    {
        var lane = new AutomationLane { Target = target, Role = role, Channel = channel };
        var value = lane.DefaultValue;
        if (target == AutomationTarget.Role && role is ChannelRole.Pan or ChannelRole.Tilt)
            value = 0.5;
        lane.Keys.Add(new Keyframe { Time = 0, Value = value });
        lane.Keys.Add(new Keyframe { Time = clip.Duration, Value = target == AutomationTarget.HueShift ? 1 : value });
        clip.Automation.Add(lane);
        state.Commit();
        return lane;
    }

    /// <summary>Adds a key at <paramref name="time"/> seconds into the clip holding the lane's current value there, and commits.</summary>
    /// <returns>The new key.</returns>
    public static Keyframe InsertKey(EditorState state, AutomationLane lane, double time, double value)
    {
        var key = new Keyframe { Time = time, Value = value };
        lane.Keys.Add(key);
        lane.SortKeys();
        state.Commit();
        return key;
    }

    /// <summary>Removes <paramref name="key"/> from <paramref name="lane"/>, clears the key selection and commits.</summary>
    public static void RemoveKey(EditorState state, AutomationLane lane, Keyframe key)
    {
        lane.Keys.Remove(key);
        state.SelectedKey = null;
        state.Commit();
    }
}
