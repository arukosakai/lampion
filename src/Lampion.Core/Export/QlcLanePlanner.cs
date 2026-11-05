using Lampion.Core.Model;

namespace Lampion.Core.Export;

/// <summary>Groups the patched fixtures the way the show's tracks will be: by first group, then one lane per loose fixture.</summary>
internal static class QlcLanePlanner
{
    /// <summary>Lanes in project group order, then loose fixtures in patch order; each fixture is in exactly one lane.</summary>
    public static List<QlcLane> Plan(Project project, Dictionary<string, QlcPatchedFixture> patch)
    {
        var lanes = new List<QlcLane>();
        var placed = new HashSet<int>();
        foreach (var group in project.Groups)
        {
            var members = group.FixtureIds.Where(patch.ContainsKey).Select(id => patch[id]).Where(p => placed.Add(p.QlcId)).ToList();
            if (members.Count > 0)
                lanes.Add(new QlcLane(group.Name, group.Color, members));
        }
        foreach (var patched in patch.Values)
            if (placed.Add(patched.QlcId))
                lanes.Add(new QlcLane(patched.Fixture.Name, "#5A7DBF", [patched]));
        return lanes;
    }
}
