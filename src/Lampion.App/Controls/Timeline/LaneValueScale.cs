using Avalonia;
using Lampion.Core.Model.Animation;

namespace Lampion.App.Controls.Timeline;

/// <summary>
/// Maps a lane's values to heights inside a clip's span on a keyframe row, with a 3-pixel inset top and
/// bottom. Shared by drawing, hit testing and keyframe dragging so they agree to the pixel.
/// </summary>
/// <param name="Min">Value drawn at the bottom.</param>
/// <param name="Max">Value drawn at the top.</param>
/// <param name="Bounds">The clip's span on the row, in control coordinates.</param>
internal readonly record struct LaneValueScale(double Min, double Max, Rect Bounds)
{
    /// <summary>The scale for <paramref name="lane"/> drawn in <paramref name="bounds"/>.</summary>
    public static LaneValueScale For(AutomationLane lane, Rect bounds)
    {
        var (min, max) = lane.DisplayRange;
        return new LaneValueScale(min, max, bounds);
    }

    /// <summary>The y coordinate of <paramref name="value"/>.</summary>
    public double ValueToY(double value) => Bounds.Bottom - 3 - (value - Min) / Math.Max(1e-6, Max - Min) * (Bounds.Height - 6);

    /// <summary>The value under y coordinate <paramref name="y"/>, unclamped; used while dragging a key.</summary>
    public double YToValue(double y) => Min + (Bounds.Bottom - 3 - y) / Math.Max(1, Bounds.Height - 6) * (Max - Min);
}
