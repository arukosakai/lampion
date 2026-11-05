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
    /// The candidate closest to <paramref name="time"/> within <paramref name="tolerance"/> seconds (strictly
    /// closer), or <paramref name="time"/> itself. Earlier candidates win ties.
    /// </summary>
    /// <param name="time">Seconds on the show timeline.</param>
    /// <param name="tolerance">Seconds; 0 disables snapping.</param>
    /// <param name="ignore">A clip whose own edges are not candidates (the one being dragged), or null.</param>
    /// <param name="beats">Whether the nearest beat is a candidate.</param>
    public double Snap(double time, double tolerance, Clip? ignore = null, bool beats = true)
    {
        var snap = new Nearest(time, tolerance);
        var project = state.Project;
        snap.Consider(0);
        snap.Consider(state.Playhead);
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
