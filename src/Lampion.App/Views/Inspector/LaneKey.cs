using Lampion.Core.Model.Animation;
using Lampion.Core.Model.Rig;

namespace Lampion.App.Views.Inspector;

/// <summary>
/// Identifies one automation lane on a clip: its target, plus the role or channel when the target needs
/// one. A clip has at most one lane per key. Lookups are allocation-free.
/// </summary>
/// <param name="Target">What the lane drives.</param>
/// <param name="Role">The driven role; only compared for <see cref="AutomationTarget.Role"/>.</param>
/// <param name="Channel">The 1-based channel; only compared for <see cref="AutomationTarget.Channel"/>.</param>
internal readonly record struct LaneKey(AutomationTarget Target, ChannelRole Role = ChannelRole.Strobe, int Channel = 1)
{
    /// <summary>The key of an existing lane.</summary>
    public static LaneKey Of(AutomationLane lane) => new(lane.Target, lane.Role, lane.Channel);

    /// <summary>The value a property shows with no lane: Core's neutral value, except pan and tilt rest at centre (0.5).</summary>
    public double DefaultValue =>
        Target == AutomationTarget.Role && Role is ChannelRole.Pan or ChannelRole.Tilt ? 0.5
        : AutomationLane.DefaultValueFor(Target);

    /// <summary>Whether <paramref name="lane"/> is the lane this key names.</summary>
    public bool Matches(AutomationLane lane) =>
        lane.Target == Target
        && (Target != AutomationTarget.Role || lane.Role == Role)
        && (Target != AutomationTarget.Channel || lane.Channel == Channel);

    /// <summary>The first matching lane in <paramref name="lanes"/>, or null.</summary>
    public AutomationLane? FindIn(List<AutomationLane> lanes)
    {
        for (var i = 0; i < lanes.Count; i++)
            if (Matches(lanes[i]))
                return lanes[i];
        return null;
    }

    /// <summary>A new, empty lane for this key.</summary>
    public AutomationLane NewLane(bool animated) => new() { Target = Target, Role = Role, Channel = Channel, Animated = animated };
}
