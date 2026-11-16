using Lampion.Core.Model.Timeline;

namespace Lampion.App.Editor;

/// <summary>Undoable edits to timeline markers, and moving the playhead between them.</summary>
internal static class MarkerEdits
{
    // "At the playhead" means within a couple of frames, so a marker the playhead was snapped to always counts.
    private const double AtPlayheadTolerance = 2 / 30.0;

    /// <summary>Adds a marker at the playhead labelled "Mn" (n = the new marker count) and commits.</summary>
    public static void AddMarker(EditorState state)
    {
        state.Project.Markers.Add(new Marker { Time = state.Playhead, Label = $"M{state.Project.Markers.Count + 1}" });
        state.Commit();
    }

    /// <summary>Deletes <paramref name="marker"/> and commits; does nothing if it's no longer in the project.</summary>
    public static void Delete(EditorState state, Marker marker)
    {
        if (state.Project.Markers.Remove(marker))
            state.Commit();
    }

    /// <summary>Deletes the marker at the playhead (within two frames) and commits.</summary>
    public static void DeleteAtPlayhead(EditorState state)
    {
        if (MarkerSearch.Nearest(state.Project.Markers, state.Playhead, AtPlayheadTolerance) is not { } marker)
            return;
        Delete(state, marker);
    }

    /// <summary>Deletes every marker and commits; one undo step brings them all back.</summary>
    public static void DeleteAll(EditorState state)
    {
        if (state.Project.Markers.Count == 0)
            return;
        state.Project.Markers.Clear();
        state.Commit();
    }

    /// <summary>Moves the playhead to the next marker after it; stays put when there is none.</summary>
    public static void GoToNext(EditorState state)
    {
        if (MarkerSearch.Next(state.Project.Markers, state.Playhead) is { } marker)
            state.Seek(marker.Time);
    }

    /// <summary>Moves the playhead to the last marker before it; stays put when there is none.</summary>
    public static void GoToPrevious(EditorState state)
    {
        if (MarkerSearch.Previous(state.Project.Markers, state.Playhead) is { } marker)
            state.Seek(marker.Time);
    }
}
