using Lampion.App.Editor;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Controls.Timeline;

/// <summary>
/// Snaps times to the things an editor lines clips up with: time 0, the playhead, markers, clip edges and
/// beats. Allocation-free, since it runs on every pointer move of a drag.
/// </summary>
internal sealed class TimelineSnapper(EditorState state, TimelineViewport viewport)
{
    /// <summary>Snap distance for drags, in screen pixels.</summary>
    public const double DragTolerancePixels = 8;

    /// <summary>
    /// The playhead when it is within <paramref name="tolerance"/> seconds (strictly closer) of <paramref name="time"/>
    /// and stopped; otherwise the closest other candidate within it, or <paramref name="time"/> itself. Earlier candidates win ties.
    /// </summary>
    /// <param name="time">Seconds on the show timeline.</param>
    /// <param name="tolerance">Seconds; 0 disables snapping.</param>
    /// <param name="ignore">A clip whose own edges are not candidates (the one being dragged), or null.</param>
    /// <param name="beats">Whether the nearest beat is a candidate.</param>
    public double Snap(double time, double tolerance, Clip? ignore = null, bool beats = true) =>
        PlayheadDistance(time) < tolerance ? state.Playhead : SnapIgnoringPlayhead(time, tolerance, ignore, beats);

    /// <summary>
    /// Where <paramref name="clip"/>, moved to start at <paramref name="start"/> on <paramref name="track"/>, should start
    /// so that one of its edges snaps. In order: an edge on the playhead (as in <see cref="Snap"/>), its start butted
    /// against the end of the previous clip on the track, otherwise whichever edge moves less.
    /// </summary>
    /// <param name="start">The clip's unsnapped start, in seconds.</param>
    /// <param name="tolerance">Seconds; 0 disables snapping.</param>
    /// <param name="clip">The clip being moved; its own edges are not candidates.</param>
    /// <param name="track">The track the clip is on now.</param>
    public double SnapMove(double start, double tolerance, Clip clip, Track track)
    {
        var duration = clip.Duration;
        var end = start + duration;
        var toStart = PlayheadDistance(start);
        var toEnd = PlayheadDistance(end);
        if (Math.Min(toStart, toEnd) < tolerance)
            return toStart <= toEnd ? state.Playhead : state.Playhead - duration;
        var previousEnd = PreviousEnd(track, start, clip);
        if (Math.Abs(previousEnd - start) < tolerance)
            return previousEnd;
        var snappedStart = SnapIgnoringPlayhead(start, tolerance, clip, beats: true);
        var snappedEnd = SnapIgnoringPlayhead(end, tolerance, clip, beats: true) - duration;
        return Math.Abs(snappedStart - start) <= Math.Abs(snappedEnd - start) ? snappedStart : snappedEnd;
    }

    // A playing playhead runs away from whatever snaps to it, so it only attracts while stopped.
    private double PlayheadDistance(double time) => state.IsPlaying ? double.PositiveInfinity : Math.Abs(state.Playhead - time);

    // The latest end among the track's clips that start before start; -infinity (never in reach) when none do.
    private static double PreviousEnd(Track track, double start, Clip moving)
    {
        var previousEnd = double.NegativeInfinity;
        foreach (var other in track.Clips)
            if (other.Id != moving.Id && other.Start < start && other.End > previousEnd)
                previousEnd = other.End;
        return previousEnd;
    }

    private double SnapIgnoringPlayhead(double time, double tolerance, Clip? ignore, bool beats)
    {
        var snap = new Nearest(time, tolerance);
        var project = state.Project;
        snap.Consider(0);
        foreach (var marker in project.Markers)
            snap.Consider(marker.Time);
        foreach (var track in project.Tracks)
            foreach (var clip in track.Clips)
            {
                if (ignore is not null && clip.Id == ignore.Id)
                    continue;
                snap.Consider(clip.Start);
                snap.Consider(clip.End);
            }
        if (beats)
        {
            var beat = state.SecondsPerBeat;
            snap.Consider(project.BeatOffset + Math.Round((time - project.BeatOffset) / beat) * beat);
        }
        return snap.Best;
    }

    /// <summary><see cref="Snap"/> with the drag tolerance (<see cref="DragTolerancePixels"/> at the current zoom).</summary>
    public double SnapDrag(double time, Clip? ignore = null, bool beats = true) =>
        Snap(time, viewport.PixelsToSeconds(DragTolerancePixels), ignore, beats);
}
