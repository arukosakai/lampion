using Lampion.Core.Model.Timeline;

namespace Lampion.App.Editor;

/// <summary>Edits that act on whatever is selected, wherever it was selected.</summary>
internal static class SelectionEdits
{
    /// <summary>
    /// Deletes the selected keyframe if there is one, otherwise the selected clips, fixtures (and their
    /// references from groups and tracks), stage object or track. Commits and clears the selection; does
    /// nothing when nothing is selected.
    /// </summary>
    public static void DeleteSelection(EditorState state)
    {
        if (state.SelectedKey is { } key)
        {
            foreach (var clip in state.Project.AllClips.OfType<LightClip>())
                foreach (var lane in clip.Automation.Where(l => l.Id == key.LaneId))
                    lane.Keys.Remove(key.Key);
            state.SelectedKey = null;
            state.Commit();
            return;
        }
        switch (state.SelectionKind)
        {
            case SelectionKind.Clip:
                foreach (var track in state.Project.Tracks)
                    track.Clips.RemoveAll(c => state.SelectedClipIds.Contains(c.Id));
                break;
            case SelectionKind.Fixture:
                state.Project.RemoveFixtures(state.SelectedFixtureIds.ToHashSet());
                break;
            case SelectionKind.StageObject:
                state.Project.Stage.Objects.RemoveAll(o => state.SelectedObjectIds.Contains(o.Id));
                break;
            case SelectionKind.Track:
                state.Project.Tracks.RemoveAll(t => t.Id == state.SelectedTrackId);
                break;
            default:
                return;
        }
        state.Commit();
        state.Deselect();
    }
}
