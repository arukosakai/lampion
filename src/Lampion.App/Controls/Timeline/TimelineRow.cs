using Lampion.Core.Model.Timeline;

namespace Lampion.App.Controls.Timeline;

/// <summary>
/// One horizontal band of the timeline: a track, or one of the keyframe rows under an expanded light track.
/// Positions are in content space (0 is the top of the first row); <see cref="TimelineViewport.ContentToY"/>
/// turns them into control coordinates.
/// </summary>
/// <param name="Track">The track the row belongs to; for keyframe rows, the track they sit under.</param>
/// <param name="IsLane">True for a keyframe row, false for the track itself.</param>
/// <param name="Lane">What the keyframe row animates; meaningless when <paramref name="IsLane"/> is false.</param>
/// <param name="Caption">Header text below the track name (its targets), or the keyframe row's label.</param>
/// <param name="TargetCount">Fixtures the track drives, for the colour-strip preview; 0 for media tracks.</param>
/// <param name="Top">Content-space top, in pixels.</param>
/// <param name="Height">Row height, in pixels.</param>
internal readonly record struct TimelineRow(Track Track, bool IsLane, LaneTarget Lane, string Caption, int TargetCount, double Top, double Height)
{
    /// <summary>Content-space bottom, in pixels.</summary>
    public double Bottom => Top + Height;
}
