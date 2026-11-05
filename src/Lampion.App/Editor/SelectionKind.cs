namespace Lampion.App.Editor;

/// <summary>What the editor's selection currently holds, which decides what Effect Controls shows.</summary>
public enum SelectionKind
{
    /// <summary>Nothing is selected.</summary>
    None,

    /// <summary>One or more timeline clips (<see cref="EditorState.SelectedClipIds"/>).</summary>
    Clip,

    /// <summary>A timeline track (<see cref="EditorState.SelectedTrackId"/>).</summary>
    Track,

    /// <summary>One or more patched fixtures (<see cref="EditorState.SelectedFixtureIds"/>).</summary>
    Fixture,

    /// <summary>A set piece on the stage (<see cref="EditorState.SelectedObjectId"/>).</summary>
    StageObject,
}
