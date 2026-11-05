using Avalonia;
using Lampion.Core.Model.Animation;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Controls.Timeline;

/// <summary>
/// Answers "what is under the pointer" against the same geometry the renderers draw: rows, header toggles,
/// clip zones and keyframes. Points are in control coordinates. Allocation-free.
/// </summary>
internal sealed class TimelineHitTester(TimelineViewport viewport, TimelineLayout layout)
{
    /// <summary>How close to a clip edge, in pixels, the pointer has to be to trim instead of move.</summary>
    public const double EdgeGrab = 6;

    private const double FadeGrab = 6;
    private const double FadeHandleBand = 10;
    private const double TransitionBand = 18;
    private const double LaneSlack = 6;
    private const double KeyGrab = 7;

    /// <summary>Finds the row at <paramref name="y"/>.</summary>
    /// <param name="y">Control y coordinate.</param>
    /// <param name="row">A copy of the row, when found.</param>
    /// <param name="rowY">The row's top in control coordinates, when found.</param>
    /// <returns>False when <paramref name="y"/> is above the first row or below the last.</returns>
    public bool TryGetRow(double y, out TimelineRow row, out double rowY)
    {
        var rows = layout.Rows;
        for (var i = 0; i < rows.Length; i++)
        {
            var top = viewport.ContentToY(rows[i].Top);
            if (y >= top && y < top + rows[i].Height)
            {
                row = rows[i];
                rowY = top;
                return true;
            }
        }
        row = default;
        rowY = 0;
        return false;
    }

    /// <summary>The header toggle under <paramref name="p"/> on track row <paramref name="row"/> (top at <paramref name="rowY"/>).</summary>
    public static HeaderPart HitHeader(in TimelineRow row, double rowY, Point p)
    {
        if (row.IsLane)
            return HeaderPart.None;
        if (TrackHeaderLayout.MuteRect(rowY).Contains(p))
            return HeaderPart.Mute;
        if (row.Track.Kind != TrackKind.Light)
            return HeaderPart.None;
        if (TrackHeaderLayout.SoloRect(rowY).Contains(p))
            return HeaderPart.Solo;
        if (TrackHeaderLayout.ExpandRect(rowY).Contains(p))
            return HeaderPart.Expand;
        return HeaderPart.None;
    }

    /// <summary>
    /// The clip, and the part of it, under <paramref name="p"/> on track row <paramref name="row"/> (top at
    /// <paramref name="rowY"/>). Later clips are drawn on top, so they are tested first.
    /// </summary>
    public bool TryHitClip(in TimelineRow row, double rowY, Point p, out ClipHit hit)
    {
        var clips = row.Track.Clips;
        var top = rowY + ClipRenderer.Inset;
        var pps = viewport.PixelsPerSecond;
        for (var i = clips.Count - 1; i >= 0; i--)
        {
            var clip = clips[i];
            var x0 = viewport.TimeToX(clip.Start);
            var x1 = viewport.TimeToX(clip.End);
            if (clip is LightClip { TransitionIn: { } transition } withTransition)
            {
                var half = transition.Duration / 2 * pps;
                if (p.X >= x0 - half && p.X <= x0 + half && p.Y >= rowY + row.Height - TransitionBand)
                {
                    hit = new ClipHit(withTransition, ClipZone.Transition);
                    return true;
                }
            }
            if (p.X < x0 - 2 || p.X > x1 + 2)
                continue;
            hit = new ClipHit(clip, Zone(clip, x0, x1, top, p, pps));
            return true;
        }
        hit = default;
        return false;
    }

    /// <summary>
    /// The first clip on keyframe row <paramref name="row"/> (top at <paramref name="rowY"/>) whose span is
    /// within a few pixels of <paramref name="p"/>, with the keyframe under the pointer if there is one.
    /// </summary>
    public bool TryHitLane(in TimelineRow row, double rowY, Point p, out LaneHit hit)
    {
        foreach (var clip in row.Track.Clips)
        {
            if (clip is not LightClip light || row.Lane.FindIn(light) is not { } lane)
                continue;
            var x0 = viewport.TimeToX(clip.Start);
            var x1 = viewport.TimeToX(clip.End);
            if (p.X < x0 - LaneSlack || p.X > x1 + LaneSlack)
                continue;
            var bounds = new Rect(x0, rowY + ClipRenderer.Inset, x1 - x0, row.Height - ClipRenderer.Inset * 2);
            var scale = LaneValueScale.For(lane, bounds);
            Keyframe? found = null;
            foreach (var key in lane.Keys)
                if (Math.Abs(viewport.TimeToX(clip.Start + key.Time) - p.X) < KeyGrab && Math.Abs(scale.ValueToY(key.Value) - p.Y) < KeyGrab)
                {
                    found = key;
                    break;
                }
            hit = new LaneHit(light, lane, found, bounds);
            return true;
        }
        hit = default;
        return false;
    }

    private static ClipZone Zone(Clip clip, double x0, double x1, double top, Point p, double pps)
    {
        if (clip is LightClip light && p.Y < top + FadeHandleBand)
        {
            if (Math.Abs(p.X - (x0 + light.FadeIn * pps)) < FadeGrab)
                return ClipZone.FadeIn;
            if (Math.Abs(p.X - (x1 - light.FadeOut * pps)) < FadeGrab)
                return ClipZone.FadeOut;
        }
        if (p.X - x0 < EdgeGrab)
            return ClipZone.TrimStart;
        if (x1 - p.X < EdgeGrab)
            return ClipZone.TrimEnd;
        return ClipZone.Body;
    }
}
