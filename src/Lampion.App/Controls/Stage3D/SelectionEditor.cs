using System.Numerics;
using Lampion.App.Editor;
using Lampion.Core.Model.Stage;

namespace Lampion.App.Controls.Stage3D;

/// <summary>
/// The stage builder's live edits to the selection: moving it from where a drag started, and aiming fixtures.
/// Each edit touches <see cref="EditorState"/> so the view updates; the drag commits the undo step on release.
/// </summary>
internal sealed class SelectionEditor(EditorState state, StagePicker picker)
{
    private readonly Dictionary<string, Vec3> _origins = [];

    /// <summary>Where the aim tool last landed on the stage, or null before the first aim.</summary>
    public Vector3? AimTarget { get; private set; }

    /// <summary>
    /// Whether the next captured move takes only the selected set piece rather than its whole structure.
    /// Set before a drag begins; the drag reads it when it captures origins.
    /// </summary>
    public bool PieceOnly { get; set; }

    /// <summary>
    /// Remembers where every selected fixture, or the selected set piece, is now; moves are relative to this.
    /// A piece of a structure brings the rest of it and its hung fixtures along, unless <see cref="PieceOnly"/>.
    /// </summary>
    public void CaptureOrigins()
    {
        _origins.Clear();
        if (state.SelectionKind == SelectionKind.StageObject && StageSelection.SelectedObject(state) is { } obj)
        {
            _origins[obj.Id] = obj.Position;
            if (!PieceOnly && obj.StructureId is { } structure)
                CaptureStructure(structure);
            return;
        }
        foreach (var id in state.SelectedFixtureIds)
            if (state.Project.FixtureById(id) is { } fixture)
                _origins[fixture.Id] = fixture.Position;
    }

    private void CaptureStructure(string id)
    {
        var project = state.Project;
        foreach (var piece in project.Stage.Objects)
            if (piece.StructureId == id)
                _origins[piece.Id] = piece.Position;
        foreach (var fixture in project.Fixtures)
            if (fixture.StructureId == id)
                _origins[fixture.Id] = fixture.Position;
    }

    /// <summary>Puts everything captured by <see cref="CaptureOrigins"/> at its origin plus <paramref name="delta"/> metres, rounded to the millimetre.</summary>
    public void MoveBy(Vec3 delta)
    {
        foreach (var (id, start) in _origins)
        {
            var target = StageMath.Round(start + delta);
            if (state.Project.FixtureById(id) is { } fixture)
                fixture.Position = target;
            else if (StageSelection.ObjectById(state, id) is { } obj)
                obj.Position = target;
        }
        state.Touch();
    }

    /// <summary>Aims the selected fixtures where the ray lands on the stage; nothing when it misses everything.</summary>
    public void AimAlong(Vector3 origin, Vector3 direction)
    {
        if (picker.SurfaceAt(origin, direction) is not { } target)
            return;
        AimTarget = target;
        var at = StageGeometry.ToVec(target);
        foreach (var id in state.SelectedFixtureIds)
            if (state.Project.FixtureById(id) is { } fixture)
                StageMath.AimAt(fixture, at);
        state.Touch();
    }
}
