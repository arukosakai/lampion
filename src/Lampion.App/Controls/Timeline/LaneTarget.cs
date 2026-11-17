using Lampion.Core.Model.Animation;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Controls.Timeline;

/// <summary>
/// What an automation lane drives, which is how the timeline groups lanes into rows: every light clip on a
/// track that animates the same thing shares one keyframe row. Fields that don't apply to the target are 0 or null.
/// </summary>
/// <param name="Target">The lane's target.</param>
/// <param name="Role">The channel role, only for <see cref="AutomationTarget.Role"/>.</param>
/// <param name="Channel">The 1-based channel, only for <see cref="AutomationTarget.Channel"/>.</param>
/// <param name="Parameter">The effect parameter key, only for <see cref="AutomationTarget.EffectParameter"/>.</param>
internal readonly record struct LaneTarget(AutomationTarget Target, ChannelRole Role, int Channel, string? Parameter = null)
{
    /// <summary>The row key of <paramref name="lane"/>.</summary>
    public static LaneTarget Of(AutomationLane lane) =>
        lane.Target switch
        {
            AutomationTarget.Role => new LaneTarget(lane.Target, lane.Role, 0),
            AutomationTarget.Channel => new LaneTarget(lane.Target, default, lane.Channel),
            AutomationTarget.EffectParameter => new LaneTarget(lane.Target, default, 0, lane.Parameter),
            _ => new LaneTarget(lane.Target, default, 0),
        };

    /// <summary>Whether <paramref name="lane"/> gets a keyframe row: animated, and not keyed to a parameter the clip's effect lacks. Allocation-free.</summary>
    public static bool IsShown(LightClip clip, AutomationLane lane) =>
        lane.Animated && (lane.Target != AutomationTarget.EffectParameter || clip.Effect.FindParameter(lane.Parameter) is not null);

    /// <summary>The first animated lane of <paramref name="clip"/> that belongs in this row, or null. Allocation-free.</summary>
    public AutomationLane? FindIn(LightClip clip)
    {
        var lanes = clip.Automation;
        for (var i = 0; i < lanes.Count; i++)
            if (IsShown(clip, lanes[i]) && Of(lanes[i]) == this)
                return lanes[i];
        return null;
    }
}
