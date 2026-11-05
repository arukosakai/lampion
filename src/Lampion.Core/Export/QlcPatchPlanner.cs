using Lampion.Core.Model;

namespace Lampion.Core.Export;

/// <summary>
/// Decides which QLC+ fixture each Lampion fixture becomes. A base-project fixture at exactly the same
/// universe and address is reused; one that partly overlaps is a conflict QLC+ would resolve by dropping
/// ours, so ours is left out with a warning instead.
/// </summary>
internal static partial class QlcPatchPlanner
{
    /// <summary>The patch, keyed by Lampion fixture id in project order; problems are added to <paramref name="warnings"/>.</summary>
    public static Dictionary<string, QlcPatchedFixture> Plan(Project project, QlcWorkspace workspace, List<string> warnings)
    {
        var ns = workspace.Ns;
        var occupants = workspace
            .Engine.Elements(ns + "Fixture")
            .Select(f => new Occupant(
                (string?)f.Element(ns + "Name") ?? "?",
                QlcWorkspace.IntOr((string?)f.Element(ns + "Universe"), 0) + 1,
                QlcWorkspace.IntOr((string?)f.Element(ns + "Address"), 0) + 1,
                Math.Max(1, QlcWorkspace.IntOr((string?)f.Element(ns + "Channels"), 1)),
                QlcWorkspace.IntOr((string?)f.Element(ns + "ID"), -1)
            ))
            .Where(o => o.QlcId >= 0)
            .ToList();
        var nextId = occupants.Select(o => o.QlcId).DefaultIfEmpty(-1).Max() + 1;
        var result = new Dictionary<string, QlcPatchedFixture>();

        for (var index = 0; index < project.Fixtures.Count; index++)
        {
            var fixture = project.Fixtures[index];
            if (project.ProfileOf(fixture) is not { } profile || profile.Channels.Count == 0)
            {
                warnings.Add($"\"{fixture.Name}\" has no fixture profile and was left out.");
                continue;
            }
            var count = profile.Channels.Count;
            if (fixture.Address < 1 || fixture.Address + count - 1 > 512 || fixture.Universe < 1)
            {
                warnings.Add($"\"{fixture.Name}\" ({Range(fixture.Universe, fixture.Address, count)}) does not fit in a universe and was left out.");
                continue;
            }
            var exact = occupants.Find(o => o.Universe == fixture.Universe && o.Address == fixture.Address);
            if (exact is not null)
            {
                if (exact.Channels != count)
                    warnings.Add($"\"{fixture.Name}\" reuses \"{exact.Name}\" at {Range(fixture.Universe, fixture.Address, count)}, which has {exact.Channels} channels instead of {count}.");
                result[fixture.Id] = new QlcPatchedFixture(fixture, index, profile, exact.QlcId, IsNew: false);
                continue;
            }
            var overlap = occupants.Find(o => o.Universe == fixture.Universe && o.Address <= fixture.Address + count - 1 && fixture.Address <= o.Address + o.Channels - 1);
            if (overlap is not null)
            {
                warnings.Add(
                    $"\"{fixture.Name}\" ({Range(fixture.Universe, fixture.Address, count)}) overlaps \"{overlap.Name}\" ({Range(overlap.Universe, overlap.Address, overlap.Channels)}); QLC+ would not load it, so it was left out."
                );
                continue;
            }
            var id = nextId++;
            occupants.Add(new Occupant(fixture.Name, fixture.Universe, fixture.Address, count, id));
            result[fixture.Id] = new QlcPatchedFixture(fixture, index, profile, id, IsNew: true);
        }
        return result;
    }

    private static string Range(int universe, int address, int channels) => $"U{universe} {address}-{address + channels - 1}";
}
