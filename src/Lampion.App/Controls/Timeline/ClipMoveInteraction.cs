using Avalonia;
using Lampion.App.Editor;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Controls.Timeline;

/// <summary>
/// Dragging a clip's body: it slides in time, snapping whichever edge is closer to a candidate, and moves to
/// another unlocked track of the same kind when the pointer crosses onto one.
/// </summary>
internal sealed class ClipMoveInteraction(
    EditorState state,
    TimelineViewport viewport,
    TimelineSnapper snapper,
    TimelineHitTester hitTester,
    Clip clip,
    Track track,
    double startTime) : ClipInteraction(state, viewport, snapper, clip, track, startTime)
{
    /// <inheritdoc />
    protected override void Apply(Point p, double delta, double tolerance)
    {
        var start = Math.Max(0, OriginalStart + delta);
        var snappedStart = Snapper.Snap(start, tolerance, Clip);
        var snappedEnd = Snapper.Snap(start + Clip.Duration, tolerance, Clip) - Clip.Duration;
        Clip.Start = Math.Max(0, Math.Abs(snappedStart - start) <= Math.Abs(snappedEnd - start) ? snappedStart : snappedEnd);
        if (hitTester.TryGetRow(p.Y, out var row, out _)
            && !row.IsLane
            && row.Track != Track
            && row.Track.Kind == Track.Kind
            && !row.Track.Locked)
        {
            Track.Clips.Remove(Clip);
            row.Track.Clips.Add(Clip);
            Track = row.Track;
        }
    }
}
