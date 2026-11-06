using System.Numerics;
using Lampion.App.Editor;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Stage;

namespace Lampion.App.Controls.Stage3D;

/// <summary>
/// The same answers as <see cref="EditorState.PrimaryFixture"/> and <see cref="EditorState.SelectedObject"/>, plus the
/// selection's centre, guaranteed allocation-free for the overlay and gizmo, which ask every frame.
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

    /// <summary>One of the selected set pieces, or null when none is selected or it no longer exists.</summary>
    public static StageObject? SelectedObject(EditorState state)
    {
        foreach (var id in state.SelectedObjectIds)
            return ObjectById(state, id);
        return null;
    }

    /// <summary>
    /// Centre of the world-axis box around the selected fixtures (their positions) or set pieces (their rotated
    /// extents), in metres; null when neither is selected.
    /// </summary>
    public static Vector3? Centre(EditorState state)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        switch (state.SelectionKind)
        {
            case SelectionKind.Fixture:
                foreach (var id in state.SelectedFixtureIds)
                    if (state.Project.FixtureById(id) is { } fixture)
                        Grow(ref min, ref max, StageGeometry.ToVector(fixture.Position), Vector3.Zero);
                break;
            case SelectionKind.StageObject:
                foreach (var id in state.SelectedObjectIds)
                    if (ObjectById(state, id) is { } obj)
                        Grow(ref min, ref max, StageGeometry.ToVector(obj.Position), StageGeometry.ToVector(StageMath.HalfExtents(obj)));
                break;
        }
        return min.X <= max.X ? (min + max) * 0.5f : null;
    }

    private static void Grow(ref Vector3 min, ref Vector3 max, Vector3 centre, Vector3 half)
    {
        min = Vector3.Min(min, centre - half);
        max = Vector3.Max(max, centre + half);
    }

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
