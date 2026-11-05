namespace Lampion.Core.Model.Rig;

/// <summary>DMX address planning and validation for a project's patch. Not for the hot path: both methods allocate.</summary>
public static class PatchPlanner
{
    private const int UniverseSize = 512;

    /// <summary>The first 1-based address in <paramref name="universe"/> where <paramref name="channels"/> consecutive channels are free, or -1.</summary>
    /// <param name="project">The project whose patch is searched.</param>
    /// <param name="universe">1-based universe.</param>
    /// <param name="channels">Footprint to place.</param>
    /// <param name="ignoreFixtureId">A fixture to treat as absent, e.g. the one being moved.</param>
    public static int NextFreeAddress(Project project, int universe, int channels, string? ignoreFixtureId = null)
    {
        var used = new bool[UniverseSize + 1];
        foreach (var fixture in project.Fixtures)
        {
            if (fixture.Universe != universe || fixture.Id == ignoreFixtureId)
                continue;
            var count = project.ProfileOf(fixture)?.Channels.Count ?? 1;
            for (var a = fixture.Address; a < fixture.Address + count && a <= UniverseSize; a++)
                if (a >= 1)
                    used[a] = true;
        }
        for (var start = 1; start + channels - 1 <= UniverseSize; start++)
        {
            var free = true;
            for (var a = start; a < start + channels; a++)
                if (used[a])
                {
                    free = false;
                    start = a;
                    break;
                }
            if (free)
                return start;
        }
        return -1;
    }

    /// <summary>Human-readable patch problems: missing profiles, overlaps and fixtures running past channel 512. Empty when the patch is clean.</summary>
    public static List<string> Problems(Project project)
    {
        var problems = new List<string>();
        var placed = project
            .Fixtures.Select(f => (Fixture: f, Count: project.ProfileOf(f)?.Channels.Count ?? 0))
            .OrderBy(x => x.Fixture.Universe)
            .ThenBy(x => x.Fixture.Address)
            .ToList();
        foreach (var (fixture, count) in placed)
        {
            if (project.ProfileOf(fixture) is null)
                problems.Add($"\"{fixture.Name}\" has no fixture profile.");
            if (fixture.Address < 1 || fixture.Address + count - 1 > UniverseSize)
                problems.Add($"\"{fixture.Name}\" ({fixture.Universe}.{fixture.Address}, {count}ch) does not fit in a universe.");
        }
        for (var i = 0; i < placed.Count; i++)
        for (var j = i + 1; j < placed.Count; j++)
        {
            var (a, ac) = placed[i];
            var (b, bc) = placed[j];
            if (a.Universe != b.Universe)
                continue;
            if (b.Address <= a.Address + ac - 1 && a.Address <= b.Address + bc - 1)
                problems.Add($"\"{a.Name}\" and \"{b.Name}\" overlap in universe {a.Universe}.");
        }
        return problems;
    }
}
