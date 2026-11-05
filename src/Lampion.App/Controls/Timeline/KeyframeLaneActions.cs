using Avalonia;
using Lampion.App.Editor;

namespace Lampion.App.Controls.Timeline;

/// <summary>
/// Clicks on keyframe rows, After Effects style: click a key to select and drag it, double-click empty
/// curve to add one, right-click a key to delete it, click elsewhere on a clip's span to select the clip.
/// </summary>
internal sealed class KeyframeLaneActions(EditorState state, TimelineViewport viewport, TimelineSnapper snapper, TimelineHitTester hitTester)
{
    /// <summary>Handles a press at <paramref name="p"/> on keyframe row <paramref name="row"/> (top at <paramref name="rowY"/>).</summary>
    /// <param name="row">The keyframe row.</param>
    /// <param name="rowY">The row's top in control coordinates.</param>
    /// <param name="p">Pointer position.</param>
    /// <param name="rightButton">True for a right-click.</param>
    /// <param name="clickCount">2 for a double-click.</param>
    /// <returns>A keyframe drag to run until release, or null when the press doesn't start one.</returns>
    public ITimelineInteraction? Press(in TimelineRow row, double rowY, Point p, bool rightButton, int clickCount)
    {
        if (!hitTester.TryHitLane(row, rowY, p, out var hit))
            return null;
        var (clip, lane, key, bounds) = hit;
        if (rightButton)
        {
            if (key is not null)
                AutomationEdits.RemoveKey(state, lane, key);
            return null;
        }
        if (key is null && clickCount == 2)
        {
            var local = viewport.XToTime(p.X) - clip.Start;
            key = AutomationEdits.InsertKey(state, lane, Math.Clamp(local, 0, clip.Duration), lane.Evaluate(local));
        }
        state.SelectClip(clip.Id);
        if (key is null)
            return null;
        state.SelectedKey = (lane.Id, key);
        state.NotifySelectionChanged();
        return new KeyframeDragInteraction(state, viewport, snapper, clip, lane, key, bounds);
    }
}
