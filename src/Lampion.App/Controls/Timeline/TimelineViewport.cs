using Avalonia;

namespace Lampion.App.Controls.Timeline;

/// <summary>
/// Zoom and scroll of the timeline, and the mapping between show time and control coordinates. Times are
/// in seconds, coordinates in device-independent pixels relative to the timeline control's top-left corner.
/// </summary>
internal sealed class TimelineViewport(Visual owner)
{
    /// <summary>Width of the track-header column on the left, in pixels.</summary>
    public const double HeaderWidth = 210;

    /// <summary>Height of the ruler along the top, in pixels.</summary>
    public const double RulerHeight = 30;

    /// <summary>Furthest zoom-out, in pixels per second.</summary>
    public const double MinPixelsPerSecond = 2;

    /// <summary>Furthest zoom-in, in pixels per second.</summary>
    public const double MaxPixelsPerSecond = 1200;

    /// <summary>Horizontal zoom; callers keep it within <see cref="MinPixelsPerSecond"/>..<see cref="MaxPixelsPerSecond"/>.</summary>
    public double PixelsPerSecond { get; set; } = 40;

    /// <summary>Show time at the left edge of the clip area, in seconds (0 or more).</summary>
    public double ScrollX { get; set; }

    /// <summary>Pixels the rows are scrolled up under the ruler (0 or more).</summary>
    public double ScrollY { get; set; }

    /// <summary>Size of the timeline control, as currently arranged.</summary>
    public Size Size => owner.Bounds.Size;

    /// <summary>Width of the whole control, header included.</summary>
    public double Width => Size.Width;

    /// <summary>Height of the whole control, ruler included.</summary>
    public double Height => Size.Height;

    /// <summary>Seconds of show time the clip area shows.</summary>
    public double VisibleSeconds => Math.Max(0, Width - HeaderWidth) / PixelsPerSecond;

    /// <summary>Where the clips are drawn: right of the headers, below the ruler.</summary>
    public Rect ClipArea => new(HeaderWidth, RulerHeight, Math.Max(0, Width - HeaderWidth), Math.Max(0, Height - RulerHeight));

    /// <summary>The x coordinate of show time <paramref name="seconds"/>.</summary>
    public double TimeToX(double seconds) => HeaderWidth + (seconds - ScrollX) * PixelsPerSecond;

    /// <summary>The show time at x coordinate <paramref name="x"/>; may be negative left of the clip area.</summary>
    public double XToTime(double x) => (x - HeaderWidth) / PixelsPerSecond + ScrollX;

    /// <summary>The y coordinate of a row whose top is <paramref name="contentTop"/> pixels below the first row's.</summary>
    public double ContentToY(double contentTop) => RulerHeight - ScrollY + contentTop;

    /// <summary>A distance of <paramref name="pixels"/> on screen, in seconds at the current zoom; used for snap tolerances.</summary>
    public double PixelsToSeconds(double pixels) => pixels / PixelsPerSecond;

    /// <summary>Zooms by <paramref name="factor"/> while keeping the time under <paramref name="anchorX"/> where it is.</summary>
    public void ZoomAround(double factor, double anchorX)
    {
        var anchorTime = XToTime(anchorX);
        PixelsPerSecond = Math.Clamp(PixelsPerSecond * factor, MinPixelsPerSecond, MaxPixelsPerSecond);
        ScrollX = Math.Max(0, anchorTime - (anchorX - HeaderWidth) / PixelsPerSecond);
    }

    /// <summary>Zooms so 0..<paramref name="contentEnd"/> seconds fill the clip area, and scrolls to the start.</summary>
    public void ZoomToFit(double contentEnd)
    {
        PixelsPerSecond = Math.Clamp((Width - HeaderWidth - 20) / Math.Max(1, contentEnd), MinPixelsPerSecond, MaxPixelsPerSecond);
        ScrollX = 0;
    }
}
