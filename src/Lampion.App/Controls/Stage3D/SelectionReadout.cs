using Lampion.App.Editor;
using Lampion.App.Rendering;
using Lampion.Core.Model.Stage;

namespace Lampion.App.Controls.Stage3D;

/// <summary>
/// The measurement panel in the stage's top-left corner: the selection's position, aim, DMX address, throw
/// and spot size in metres, plus a hint for the active tool. Recomposed each frame, reallocated only on change.
/// </summary>
internal sealed class SelectionReadout(StageView view, EditorState state)
{
    private readonly CachedText _text = new();
    private int _lines;

    /// <summary>The panel's lines joined with newlines, or null when there is nothing to show.</summary>
    /// <param name="scene">The frame's scene, for the selected fixture's throw.</param>
    public string? Compose(SceneSnapshot scene)
    {
        _text.Clear();
        _lines = 0;
        if (state.SelectionKind == SelectionKind.Fixture && StageSelection.PrimaryFixture(state) is { } fixture)
        {
            var count = state.SelectedFixtureIds.Count;
            Line().Append(fixture.Name);
            if (count > 1)
                _text.Append(" (+").Append(count - 1).Append(" more)");
            Position(fixture.Position);
            Line().Append("Yaw ").Append(fixture.Yaw, "0.#").Append("°   Pitch ").Append(fixture.Pitch, "0.#").Append("°   Roll ").Append(fixture.Roll, "0.#").Append("°   DMX ").Append(fixture.Universe).Append(".").Append(fixture.Address);
            if (FindVisual(scene, fixture.Id, out var visual))
            {
                var spot = 2 * visual.BeamLength * MathF.Tan(visual.BeamAngle * 0.5f * MathF.PI / 180);
                Line().Append("Throw ").AppendMetres(visual.BeamLength).Append("   Spot Ø ").AppendMetres(spot).Append(" (").Append(visual.BeamAngle, "0.#").Append("°)");
            }
        }
        else if (state.SelectionKind == SelectionKind.StageObject && StageSelection.SelectedObject(state) is { } obj)
        {
            Line().Append(obj.Name).Append(" (").Append(KindName(obj.Kind)).Append(")");
            var count = state.SelectedObjectIds.Count;
            if (count > 1)
                _text.Append(" (+").Append(count - 1).Append(" more)");
            if (StructureEdits.Find(state.Project, obj.StructureId) is { } structure)
                _text.Append(" · part of ").Append(structure.Name).Append(view.BuildMode ? " (Ctrl+drag moves just this piece)" : "");
            Position(obj.Position);
            Line().Append("W ").AppendMetres(obj.Size.X).Append("   H ").AppendMetres(obj.Size.Y).Append("   D ").AppendMetres(obj.Size.Z);
            Line().Append("Yaw ").Append(obj.Yaw, "0.#").Append("°   Pitch ").Append(obj.Pitch, "0.#").Append("°   Roll ").Append(obj.Roll, "0.#").Append("°");
            var halfHeight = StageMath.HalfHeight(obj);
            Line().Append("Top at ").AppendMetres(obj.Position.Y + halfHeight).Append("   bottom at ").AppendMetres(obj.Position.Y - halfHeight);
        }
        if (view.BuildMode)
        {
            if (view.Tool == BuilderTool.Aim)
                Line().Append("Aim: click/drag on the stage to point the selected fixtures");
            else
                Line().Append("Drag to move · Shift+click multi-select · Alt+drag height · arrows lock an axis · snap ").AppendMetres(state.Project.Stage.GridStep);
        }
        return _lines == 0 ? null : _text.ToText();
    }

    private CachedText Line()
    {
        if (_lines++ > 0)
            _text.Append("\n");
        return _text;
    }

    private void Position(Vec3 p) => Line().Append("X ").AppendMetres(p.X).Append("   Y ").AppendMetres(p.Y).Append("   Z ").AppendMetres(p.Z);

    private static bool FindVisual(SceneSnapshot scene, string id, out FixtureVisual visual)
    {
        var fixtures = scene.Fixtures;
        for (var i = 0; i < fixtures.Length; i++)
            if (fixtures[i].Id == id)
            {
                visual = fixtures[i];
                return true;
            }
        visual = default;
        return false;
    }

    // Enum.ToString would box every frame; these are the names the readout has always shown.
    private static string KindName(StageObjectKind kind) =>
        kind switch
        {
            StageObjectKind.Truss => "Truss",
            StageObjectKind.Platform => "Platform",
            StageObjectKind.Wall => "Wall",
            StageObjectKind.Screen => "Screen",
            _ => kind.ToString(),
        };
}
