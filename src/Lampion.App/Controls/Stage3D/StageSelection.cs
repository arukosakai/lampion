using Lampion.App.Editor;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Stage;

namespace Lampion.App.Controls.Stage3D;

/// <summary>
/// The same answers as <see cref="EditorState.PrimaryFixture"/> and <see cref="EditorState.SelectedObject"/>, guaranteed
/// allocation-free for the overlay and gizmo, which ask every frame.
/// </summary>
internal static class StageSelection
{
    /// <summary>The first selected fixture, or null when none is selected or it no longer exists.</summary>
    public static Fixture? PrimaryFixture(EditorState state)
    {
        foreach (var id in state.SelectedFixtureIds)
            return state.Project.FixtureById(id);
        return null;
    }

    /// <summary>The selected set piece, or null.</summary>
    public static StageObject? SelectedObject(EditorState state) => state.SelectedObjectId is { } id ? ObjectById(state, id) : null;

    /// <summary>The set piece with this id, or null.</summary>
    public static StageObject? ObjectById(EditorState state, string id)
    {
        var objects = state.Project.Stage.Objects;
        for (var i = 0; i < objects.Count; i++)
            if (objects[i].Id == id)
                return objects[i];
        return null;
    }
}
