using Avalonia;
using Avalonia.Media;

namespace Lampion.App.Controls.Timeline;

/// <summary>Where the clickable parts of a track header sit, shared by drawing and hit testing.</summary>
internal static class TrackHeaderLayout
{
    /// <summary>Left edge of the track name, in pixels from the header's left.</summary>
    public const double NameX = 26;

    /// <summary>Font size of the track name.</summary>
    public const double NameSize = 12;

    /// <summary>Font weight of the track name.</summary>
    public const FontWeight NameWeight = FontWeight.SemiBold;

    /// <summary>The mute toggle of a header whose top is at <paramref name="rowY"/>.</summary>
    public static Rect MuteRect(double rowY) => new(TimelineViewport.HeaderWidth - 48, rowY + 6, 18, 18);

    /// <summary>The solo toggle (light tracks only) of a header whose top is at <paramref name="rowY"/>.</summary>
    public static Rect SoloRect(double rowY) => new(TimelineViewport.HeaderWidth - 26, rowY + 6, 18, 18);

    /// <summary>
    /// The twirl-down (light tracks only) of a header whose top is at <paramref name="rowY"/>: the arrow and the
    /// track's <paramref name="name"/> after it, stopping short of the mute toggle.
    /// </summary>
    public static Rect ExpandRect(double rowY, string name)
    {
        var nameRight = NameX + TextCache.Get(name, NameSize, TimelinePalette.Text, NameWeight).Width + 4;
        var right = Math.Min(nameRight, MuteRect(rowY).X - 4);
        return new Rect(4, rowY + 4, right - 4, 22);
    }
}
