using Avalonia;
using Lampion.App.Editor;
using Lampion.Core.Model.Timeline;
using Lampion.Core.Persistence;

namespace Lampion.App.Controls.Timeline;

/// <summary>
/// Drops from the Project and Effects panels: effects and presets onto light tracks, fixtures and groups
/// onto (or below) tracks, transitions onto light clips, plus the preview of where a drop would land.
/// Points are in timeline-control coordinates.
/// </summary>
internal sealed class TimelineDropTarget(EditorState state, TimelineViewport viewport, TimelineLayout layout, TimelineHitTester hitTester, TimelineSnapper snapper)
{
    /// <summary>Where the drag being previewed would land, or null when nothing is previewed or it can't land here.</summary>
    public Rect? Preview { get; private set; }

    /// <summary>Updates <see cref="Preview"/> for a drag of <paramref name="kind"/> at <paramref name="p"/>; null clears it.</summary>
    /// <param name="p">Pointer position, or null to clear.</param>
    /// <param name="kind">What is being dragged.</param>
    /// <param name="length">Seconds an effect clip would last.</param>
    public void UpdatePreview(Point? p, DropKind kind, double length) =>
        Preview = p is { } point ? PreviewRect(point, kind, length) : null;

    /// <summary>Adds a <paramref name="effectKind"/> clip on the light track under <paramref name="p"/>, at the snapped time.</summary>
    /// <returns>False (and no edit) when <paramref name="p"/> isn't over a light track's clip area.</returns>
    public bool DropEffect(string effectKind, Point p)
    {
        if (!TryGetClipSlot(p, out var track, out var start))
            return false;
        TimelineOps.AddEffectClip(state, track, effectKind, start);
        return true;
    }

    /// <summary>Adds a clip made from <paramref name="preset"/> on the light track under <paramref name="p"/>, at the snapped time.</summary>
    /// <returns>False (and no edit) when <paramref name="p"/> isn't over a light track's clip area.</returns>
    public bool DropPreset(EffectPreset preset, Point p)
    {
        if (!TryGetClipSlot(p, out var track, out var start))
            return false;
        TimelineOps.AddPresetClip(state, track, preset, start);
        return true;
    }

    /// <summary>
    /// Fixtures or groups: onto a light track (or its keyframe rows) they join its targets, anywhere else
    /// below the ruler they get a new light track named <paramref name="trackName"/>.
    /// </summary>
    /// <returns>False when <paramref name="p"/> is outside the track area or nothing was dragged.</returns>
    public bool DropTargets(IReadOnlyCollection<string> fixtureIds, IReadOnlyCollection<string> groupIds, string trackName, Point p)
    {
        if (p.Y <= TimelineViewport.RulerHeight || p.Y > viewport.Height || p.X < 0 || p.X > viewport.Width || fixtureIds.Count + groupIds.Count == 0)
            return false;
        if (hitTester.TryGetRow(p.Y, out var row, out _) && row.Track.Kind == TrackKind.Light)
        {
            TrackEdits.AddTargets(state, row.Track, fixtureIds, groupIds);
            return true;
        }
        var track = TimelineOps.AddLightTrack(state, fixtureIds, groupIds);
        track.Name = trackName;
        state.Commit();
        return true;
    }

    /// <summary>Gives the light clip under <paramref name="p"/> a transition in of <paramref name="kind"/> and selects it.</summary>
    /// <returns>False (and no edit) when there's no light clip under <paramref name="p"/>.</returns>
    public bool DropTransition(TransitionKind kind, Point p)
    {
        if (!TryHitLightClip(p, out var clip, out _, out _))
            return false;
        TimelineOps.SetTransition(state, clip, kind);
        state.SelectClip(clip.Id);
        return true;
    }

    private Rect? PreviewRect(Point p, DropKind kind, double length)
    {
        var hasRow = hitTester.TryGetRow(p.Y, out var row, out var rowY);
        var pps = viewport.PixelsPerSecond;
        switch (kind)
        {
            case DropKind.Effect when TryGetClipSlot(p, out _, out var start):
                return new Rect(viewport.TimeToX(start), rowY + 2, length * pps, row.Height - 4);
            case DropKind.Targets when hasRow && row.Track.Kind == TrackKind.Light:
                return new Rect(0, rowY + 1, viewport.Width, TimelineLayout.TrackHeight(row.Track) - 2);
            case DropKind.Targets when p.Y > TimelineViewport.RulerHeight:
                // Below the tracks (or on a media track): a new light track will be made.
                var bottom = layout.LightTracksBottom() is { } contentBottom ? viewport.ContentToY(contentBottom) : TimelineViewport.RulerHeight;
                return new Rect(0, bottom - 2, viewport.Width, 4);
            case DropKind.Transition when TryHitLightClip(p, out var clip, out var clipRow, out var clipRowY):
                var cut = viewport.TimeToX(clip.Start);
                var half = Math.Max(10, state.SecondsPerBeat * pps);
                return new Rect(cut - half, clipRowY + 2, Math.Min(half * 2, Math.Max(0, viewport.Width - TimelineViewport.HeaderWidth)), clipRow.Height - 4);
            default:
                return null;
        }
    }

    /// <summary>The light track and snapped start time for a clip dropped at <paramref name="p"/>.</summary>
    private bool TryGetClipSlot(Point p, out Track track, out double start)
    {
        if (p.X > TimelineViewport.HeaderWidth && hitTester.TryGetRow(p.Y, out var row, out _) && !row.IsLane && row.Track.Kind == TrackKind.Light)
        {
            track = row.Track;
            start = snapper.SnapDrag(Math.Max(0, viewport.XToTime(p.X)));
            return true;
        }
        track = null!;
        start = 0;
        return false;
    }

    private bool TryHitLightClip(Point p, out LightClip clip, out TimelineRow row, out double rowY)
    {
        if (p.X > TimelineViewport.HeaderWidth
            && hitTester.TryGetRow(p.Y, out row, out rowY)
            && !row.IsLane
            && hitTester.TryHitClip(row, rowY, p, out var hit)
            && hit.Clip is LightClip light)
        {
            clip = light;
            return true;
        }
        clip = null!;
        row = default;
        rowY = 0;
        return false;
    }
}
