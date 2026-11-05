using Avalonia;
using Lampion.App.Editor;

namespace Lampion.App.Controls.Timeline;

/// <summary>Clicks on track headers: the mute, solo and twirl-down toggles, selecting the track, and its context menu.</summary>
internal sealed class TrackHeaderActions(EditorState state, TimelineContextMenus menus, TimelineControl host)
{
    /// <summary>Handles a press at <paramref name="p"/> on the header of <paramref name="row"/> (top at <paramref name="rowY"/>).</summary>
    /// <param name="row">The row under the pointer.</param>
    /// <param name="rowY">The row's top in control coordinates.</param>
    /// <param name="p">Pointer position.</param>
    /// <param name="rightButton">True for a right-click, which opens the track menu (keyframe rows included).</param>
    public void Press(in TimelineRow row, double rowY, Point p, bool rightButton)
    {
        var track = row.Track;
        if (rightButton)
        {
            state.SelectTrack(track.Id);
            menus.TrackMenu(track, state.Playhead).Open(host);
            return;
        }
        if (row.IsLane)
            return;
        switch (TimelineHitTester.HitHeader(row, rowY, p))
        {
            case HeaderPart.Mute:
                TrackEdits.ToggleMute(state, track);
                break;
            case HeaderPart.Solo:
                TrackEdits.ToggleSolo(state, track);
                break;
            case HeaderPart.Expand:
                TrackEdits.ToggleExpanded(state, track);
                host.NotifyViewChanged();
                break;
            default:
                state.SelectTrack(track.Id);
                break;
        }
    }
}
