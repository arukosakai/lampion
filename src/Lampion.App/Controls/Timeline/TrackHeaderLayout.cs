using Avalonia;

namespace Lampion.App.Controls.Timeline;

/// <summary>Where the clickable parts of a track header sit, shared by drawing and hit testing.</summary>
internal static class TrackHeaderLayout
{
    /// <summary>The mute toggle of a header whose top is at <paramref name="rowY"/>.</summary>
    public static Rect MuteRect(double rowY) => new(TimelineViewport.HeaderWidth - 48, rowY + 6, 18, 18);

    /// <summary>The solo toggle (light tracks only) of a header whose top is at <paramref name="rowY"/>.</summary>
    public static Rect SoloRect(double rowY) => new(TimelineViewport.HeaderWidth - 26, rowY + 6, 18, 18);

    /// <summary>The twirl-down (light tracks only) of a header whose top is at <paramref name="rowY"/>.</summary>
    public static Rect ExpandRect(double rowY) => new(4, rowY + 4, 20, 22);
}
