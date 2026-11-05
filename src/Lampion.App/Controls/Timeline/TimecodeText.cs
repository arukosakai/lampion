using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace Lampion.App.Controls.Timeline;

/// <summary>
/// <c>HH:MM:SS:FF</c> timecodes. <see cref="Draw"/> lays the code out from cached two-digit pieces, so
/// the playhead readout that changes every frame never formats a string.
/// </summary>
internal static class TimecodeText
{
    /// <summary>The frame rate the timeline counts in.</summary>
    public const int DefaultFps = 30;

    private static readonly string[] TwoDigits = BuildTwoDigits();

    /// <summary>The timecode as a new string, e.g. "00:01:05:12". Allocates; for cold UI such as the inspector.</summary>
    /// <param name="seconds">Show time, 0 or more.</param>
    /// <param name="fps">Frames per second for the last field.</param>
    public static string Format(double seconds, int fps = DefaultFps)
    {
        Split(seconds, fps, out var hours, out var minutes, out var secs, out var frames);
        return $"{hours:00}:{minutes:00}:{secs:00}:{frames:00}";
    }

    /// <summary>Draws the timecode of <paramref name="seconds"/> at <paramref name="at"/>; allocation-free below 100 hours.</summary>
    /// <param name="ctx">Where to draw.</param>
    /// <param name="seconds">Show time, 0 or more.</param>
    /// <param name="at">Top-left corner.</param>
    /// <param name="brush">A shared brush (it's part of the text cache key).</param>
    /// <param name="size">Font size in pixels.</param>
    /// <param name="weight">Font weight.</param>
    public static void Draw(DrawingContext ctx, double seconds, Point at, IBrush brush, double size, FontWeight weight)
    {
        Split(seconds, DefaultFps, out var hours, out var minutes, out var secs, out var frames);
        var x = at.X;
        x = Piece(ctx, hours < 100 ? TwoDigits[hours] : hours.ToString("00", CultureInfo.CurrentCulture), x, at.Y, brush, size, weight);
        x = Piece(ctx, ":", x, at.Y, brush, size, weight);
        x = Piece(ctx, TwoDigits[minutes], x, at.Y, brush, size, weight);
        x = Piece(ctx, ":", x, at.Y, brush, size, weight);
        x = Piece(ctx, TwoDigits[secs], x, at.Y, brush, size, weight);
        x = Piece(ctx, ":", x, at.Y, brush, size, weight);
        Piece(ctx, TwoDigits[frames], x, at.Y, brush, size, weight);
    }

    private static void Split(double seconds, int fps, out long hours, out long minutes, out long secs, out long frames)
    {
        var totalFrames = (long)Math.Round(seconds * fps);
        frames = totalFrames % fps;
        var s = totalFrames / fps;
        hours = s / 3600;
        minutes = s / 60 % 60;
        secs = s % 60;
    }

    private static double Piece(DrawingContext ctx, string text, double x, double y, IBrush brush, double size, FontWeight weight)
    {
        var formatted = TextCache.Get(text, size, brush, weight);
        ctx.DrawText(formatted, new Point(x, y));
        return x + formatted.WidthIncludingTrailingWhitespace;
    }

    private static string[] BuildTwoDigits()
    {
        var digits = new string[100];
        for (var i = 0; i < digits.Length; i++)
            digits[i] = i.ToString("00", CultureInfo.CurrentCulture);
        return digits;
    }
}
