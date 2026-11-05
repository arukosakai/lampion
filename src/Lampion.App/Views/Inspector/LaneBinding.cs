using Lampion.App.Editor;
using Lampion.Core.Model.Animation;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Views.Inspector;

/// <summary>
/// The model side of an animatable property: one lane on one clip, read and written at the playhead the
/// way After Effects does it. No lane means the default value; a static lane holds one value; an animated
/// lane holds keyframes. Mutators don't notify; wrap them in <see cref="InspectorContext.Edit(Action, bool)"/>.
/// </summary>
/// <param name="state">The editor state, for the playhead and seeking.</param>
/// <param name="clip">The clip that owns the lane.</param>
/// <param name="key">Which lane.</param>
internal sealed class LaneBinding(EditorState state, LightClip clip, LaneKey key)
{
    // Keys closer than this to the playhead count as "at" it (seconds).
    private const double KeyTolerance = 1e-3;

    /// <summary>The lane, or null while the property is at its default. Allocation-free.</summary>
    public AutomationLane? Lane => key.FindIn(clip.Automation);

    /// <summary>Whether the stopwatch is on.</summary>
    public bool IsAnimated => Lane is { Animated: true };

    /// <summary>The playhead in clip-local seconds, clamped to the clip.</summary>
    public double LocalTime => Math.Clamp(state.Playhead - clip.Start, 0, clip.Duration);

    /// <summary>The property's value at the playhead. Allocation-free.</summary>
    public double Current => Lane is { } lane ? lane.Evaluate(LocalTime) : key.DefaultValue;

    /// <summary>The keyframe at the playhead on an animated lane, or null. Allocation-free.</summary>
    public Keyframe? KeyAtPlayhead()
    {
        if (Lane is not { Animated: true } lane)
            return null;
        var local = LocalTime;
        var keys = lane.Keys;
        for (var i = 0; i < keys.Count; i++)
            if (Math.Abs(keys[i].Time - local) < KeyTolerance)
                return keys[i];
        return null;
    }

    /// <summary>Sets the value: the static value with the stopwatch off, else the key at the playhead (adding one if needed).</summary>
    public void Set(double value)
    {
        var lane = Lane;
        if (lane is null)
        {
            lane = key.NewLane(animated: false);
            clip.Automation.Add(lane);
        }
        if (!lane.Animated)
        {
            lane.Keys.Clear();
            lane.Keys.Add(new Keyframe { Time = 0, Value = value });
            return;
        }
        if (KeyAtPlayhead() is { } existing)
            existing.Value = value;
        else
        {
            lane.Keys.Add(new Keyframe { Time = LocalTime, Value = value });
            lane.SortKeys();
        }
    }

    /// <summary>
    /// Flips the stopwatch. Turning it on keys the current value at the playhead and twirls the track open;
    /// turning it off keeps the current value as the static one.
    /// </summary>
    /// <param name="keepLane">When false, a lane left at its default value is removed altogether.</param>
    public void ToggleStopwatch(bool keepLane)
    {
        var value = Current;
        var lane = Lane;
        if (lane is { Animated: true })
        {
            lane.Animated = false;
            lane.Keys.Clear();
            lane.Keys.Add(new Keyframe { Value = value });
            if (!keepLane && Math.Abs(value - key.DefaultValue) < 1e-6)
                clip.Automation.Remove(lane);
            return;
        }
        if (lane is null)
        {
            lane = key.NewLane(animated: true);
            clip.Automation.Add(lane);
        }
        lane.Animated = true;
        lane.Keys.Clear();
        lane.Keys.Add(new Keyframe { Time = LocalTime, Value = value });
        if (state.FindClip(clip.Id) is { } found)
            found.Track.Expanded = true;
    }

    /// <summary>The ◆ button: removes the key at the playhead (never the last one) or adds one with the current value.</summary>
    public void ToggleKeyAtPlayhead()
    {
        if (Lane is not { } lane)
            return;
        if (KeyAtPlayhead() is { } existing)
        {
            if (lane.Keys.Count > 1)
                lane.Keys.Remove(existing);
        }
        else
            Set(Current);
    }

    /// <summary>Moves the playhead to the next or previous keyframe, if there is one.</summary>
    public void Jump(bool forward)
    {
        if (Lane is not { Keys.Count: > 0 } lane)
            return;
        var local = state.Playhead - clip.Start;
        var keys = lane.Keys;
        Keyframe? target = null;
        if (forward)
        {
            for (var i = 0; i < keys.Count && target is null; i++)
                if (keys[i].Time > local + KeyTolerance)
                    target = keys[i];
        }
        else
        {
            for (var i = keys.Count - 1; i >= 0 && target is null; i--)
                if (keys[i].Time < local - KeyTolerance)
                    target = keys[i];
        }
        if (target is not null)
            state.Seek(clip.Start + target.Time);
    }

    /// <summary>Removes every lane with this key from the clip.</summary>
    public void Remove()
    {
        for (var i = clip.Automation.Count - 1; i >= 0; i--)
            if (key.Matches(clip.Automation[i]))
                clip.Automation.RemoveAt(i);
    }
}
