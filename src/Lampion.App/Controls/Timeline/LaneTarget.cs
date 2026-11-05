using Lampion.Core.Model.Animation;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Controls.Timeline;

/// <summary>
/// What an automation lane drives, which is how the timeline groups lanes into rows: every light clip on a
/// track that animates the same thing shares one keyframe row. Fields that don't apply to the target are 0.
/// </summary>
/// <param name="Target">The lane's target.</param>
/// <param name="Role">The channel role, only for <see cref="AutomationTarget.Role"/>.</param>
/// <param name="Channel">The 1-based channel, only for <see cref="AutomationTarget.Channel"/>.</param>
internal readonly record struct LaneTarget(AutomationTarget Target, ChannelRole Role, int Channel)
{
    /// <summary>The row key of <paramref name="lane"/>.</summary>
    public static LaneTarget Of(AutomationLane lane) =>
        lane.Target switch
        {
            AutomationTarget.Role => new LaneTarget(lane.Target, lane.Role, 0),
            AutomationTarget.Channel => new LaneTarget(lane.Target, default, lane.Channel),
            _ => new LaneTarget(lane.Target, default, 0),
        };

    /// <summary>The first animated lane of <paramref name="clip"/> that belongs in this row, or null. Allocation-free.</summary>
    public AutomationLane? FindIn(LightClip clip)
    {
        var lanes = clip.Automation;
        for (var i = 0; i < lanes.Count; i++)
            if (lanes[i].Animated && Of(lanes[i]) == this)
                return lanes[i];
        return null;
    }
}
