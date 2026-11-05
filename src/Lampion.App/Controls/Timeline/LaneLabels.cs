using Lampion.Core.Model.Animation;

namespace Lampion.App.Controls.Timeline;

/// <summary>
/// Keyframe-row captions ("◆ Pan channel"), built once per <see cref="LaneTarget"/> because
/// <see cref="AutomationLane.Label"/> allocates. UI thread only.
/// </summary>
internal static class LaneLabels
{
    private static readonly Dictionary<LaneTarget, string> Captions = [];

    /// <summary>The caption of the row <paramref name="lane"/> belongs to; allocation-free after the first call per target.</summary>
    public static string Caption(AutomationLane lane, LaneTarget target)
    {
        if (!Captions.TryGetValue(target, out var caption))
        {
            caption = "◆ " + lane.Label;
            Captions[target] = caption;
        }
        return caption;
    }
}
