using Lampion.Core.Model.Timeline;

namespace Lampion.App.Editor;

/// <summary>Undoable edits to timeline markers.</summary>
internal static class MarkerEdits
{
    /// <summary>Adds a marker at the playhead labelled "Mn" (n = the new marker count) and commits.</summary>
    public static void AddMarker(EditorState state)
    {
        state.Project.Markers.Add(new Marker { Time = state.Playhead, Label = $"M{state.Project.Markers.Count + 1}" });
        state.Commit();
    }
}
