using Lampion.Core.Model.Rig;

namespace Lampion.Core.Model.Stage;

/// <summary>
/// Edits on a <see cref="Structure"/> as a whole: making one, moving and turning it with its hung fixtures,
/// ungrouping and deleting. None of them commit; callers wrap them in an undo step.
/// </summary>
public static class StructureEdits
{
    /// <summary>Groups <paramref name="pieces"/> and the <paramref name="fixtures"/> hung on them into a new structure.</summary>
    /// <param name="project">The project the pieces and fixtures are (or are about to be) in.</param>
    /// <param name="name">Name for the structure.</param>
    /// <param name="pieces">Its set pieces; any earlier structure they were in is replaced.</param>
    /// <param name="fixtures">Fixtures that move with it.</param>
    /// <param name="yaw">How far it's already turned, degrees, for the inspector's Turn value.</param>
    public static Structure Create(Project project, string name, IEnumerable<StageObject> pieces, IEnumerable<Fixture> fixtures, double yaw = 0)
    {
        var structure = new Structure { Name = name, Yaw = yaw };
        project.Stage.Structures.Add(structure);
        foreach (var piece in pieces)
            piece.StructureId = structure.Id;
        foreach (var fixture in fixtures)
            fixture.StructureId = structure.Id;
        return structure;
    }

    /// <summary>The structure with this id, or null (also for a null id).</summary>
    public static Structure? Find(Project project, string? id)
    {
        if (id is null)
            return null;
        foreach (var structure in project.Stage.Structures)
            if (structure.Id == id)
                return structure;
        return null;
    }

    /// <summary>The set pieces in the structure, in drawing order.</summary>
    public static IEnumerable<StageObject> Pieces(Project project, string id) => project.Stage.Objects.Where(o => o.StructureId == id);

    /// <summary>The fixtures hung on the structure, in patch order.</summary>
    public static IEnumerable<Fixture> Fixtures(Project project, string id) => project.Fixtures.Where(f => f.StructureId == id);

    /// <summary>Middle of the pieces' centres in plan (X and Z, metres); Y is 0. The origin when it has no pieces.</summary>
    public static Vec3 Centre(Project project, string id)
    {
        double minX = double.MaxValue, maxX = double.MinValue, minZ = double.MaxValue, maxZ = double.MinValue;
        foreach (var piece in Pieces(project, id))
        {
            minX = Math.Min(minX, piece.Position.X);
            maxX = Math.Max(maxX, piece.Position.X);
            minZ = Math.Min(minZ, piece.Position.Z);
            maxZ = Math.Max(maxZ, piece.Position.Z);
        }
        return minX > maxX ? default : StageMath.Round(new Vec3((minX + maxX) / 2, 0, (minZ + maxZ) / 2));
    }

    /// <summary>Shifts every piece and hung fixture by <paramref name="delta"/> metres.</summary>
    public static void MoveBy(Project project, string id, Vec3 delta)
    {
        foreach (var piece in Pieces(project, id))
            piece.Position = StageMath.Round(piece.Position + delta);
        foreach (var fixture in Fixtures(project, id))
            fixture.Position = StageMath.Round(fixture.Position + delta);
    }

    /// <summary>
    /// Turns the structure <paramref name="degrees"/> around the vertical through its <see cref="Centre"/>,
    /// pieces and hung fixtures alike, so the fixtures keep pointing the same way relative to it.
    /// </summary>
    public static void Turn(Project project, string id, double degrees)
    {
        var centre = Centre(project, id);
        var turn = Basis.FromYawPitchRoll(degrees, 0, 0);
        Vec3 Around(Vec3 p) => StageMath.Round(centre + turn.ToWorld(p - centre));
        foreach (var piece in Pieces(project, id))
        {
            piece.Position = Around(piece.Position);
            piece.Yaw = Math.Round(piece.Yaw + degrees, 2);
        }
        foreach (var fixture in Fixtures(project, id))
        {
            fixture.Position = Around(fixture.Position);
            fixture.Yaw = Math.Round(fixture.Yaw + degrees, 2);
        }
        if (Find(project, id) is { } structure)
            structure.Yaw = Math.Round(structure.Yaw + degrees, 2);
    }

    /// <summary>Dissolves the structure: its pieces and fixtures stay where they are, just no longer move together.</summary>
    public static void Ungroup(Project project, string id)
    {
        foreach (var piece in Pieces(project, id))
            piece.StructureId = null;
        foreach (var fixture in Fixtures(project, id))
            fixture.StructureId = null;
        project.Stage.Structures.RemoveAll(s => s.Id == id);
    }

    /// <summary>Removes the structure and its pieces; its fixtures too when <paramref name="withFixtures"/>, otherwise they stay, unhung.</summary>
    public static void Delete(Project project, string id, bool withFixtures)
    {
        if (withFixtures)
            project.RemoveFixtures(Fixtures(project, id).Select(f => f.Id).ToHashSet());
        project.Stage.Objects.RemoveAll(o => o.StructureId == id);
        Ungroup(project, id);
    }
}
