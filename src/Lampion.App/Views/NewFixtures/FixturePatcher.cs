using Lampion.App.Editor;
using Lampion.App.Views.ProjectBin;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Stage;

namespace Lampion.App.Views.NewFixtures;

/// <summary>Adds a batch of fixtures of one profile to the patch, the way a console's auto-patch does.</summary>
internal static class FixturePatcher
{
    private const int UniverseSize = 512;
    private const int MaxUniverse = 64;

    /// <summary>
    /// Patches up to <paramref name="count"/> fixtures from <paramref name="universe"/> on, continuing in the
    /// next universe when one is full and stopping after universe 64. Commits one undo step.
    /// </summary>
    /// <param name="state">The project to patch into.</param>
    /// <param name="profile">The fixtures' profile.</param>
    /// <param name="count">How many to add; ≥ 1.</param>
    /// <param name="universe">First universe to try, 1-based.</param>
    /// <param name="startAddress">First address (1-based), or null for the first free one.</param>
    /// <param name="name">Name of a single fixture, or the prefix numbered fixtures and the group share.</param>
    /// <param name="truss">Truss to hang them along, or null for a row at 5.5 m.</param>
    /// <param name="group">Also put them in a new group (only when more than one is patched).</param>
    /// <returns>Ids of the fixtures added, in patch order; may be fewer than <paramref name="count"/>.</returns>
    public static List<string> Patch(EditorState state, FixtureProfile profile, int count, int universe, int? startAddress, string name, StageObject? truss, bool group)
    {
        var project = state.Project;
        var footprint = profile.Channels.Count;
        var fixtures = new List<Fixture>();
        var address = startAddress ?? PatchPlanner.NextFreeAddress(project, universe, footprint);
        for (var i = 0; i < count; i++)
        {
            while (address < 0 || address + footprint - 1 > UniverseSize)
            {
                if (++universe > MaxUniverse)
                    break;
                address = PatchPlanner.NextFreeAddress(project, universe, footprint);
            }
            if (universe > MaxUniverse)
                break;
            var fixture = new Fixture
            {
                Name = count == 1 ? name : $"{name} {i + 1}",
                ProfileId = profile.Id,
                Universe = universe,
                Address = address,
                Position = new Vec3(-(count - 1) / 2.0 + i, 5.5, 0),
                Pitch = profile.HasPanTilt ? -90 : -60,
            };
            project.Fixtures.Add(fixture);
            fixtures.Add(fixture);
            address = PatchPlanner.NextFreeAddress(project, universe, footprint);
        }
        if (truss is not null)
            StageMath.DistributeAlong(truss, fixtures);
        if (group && fixtures.Count > 1)
        {
            var created = new FixtureGroup { Name = name, Color = GroupPalette.ForIndex(project.Groups.Count) };
            created.FixtureIds.AddRange(fixtures.Select(f => f.Id));
            project.Groups.Add(created);
        }
        state.Commit();
        return fixtures.Select(f => f.Id).ToList();
    }
}
