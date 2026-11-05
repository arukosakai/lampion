using Lampion.App.Editor;
using Lampion.App.Views.ProjectBin;
using Lampion.Core.Model;
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
        var fixtures = Add(project, profile, count, universe, startAddress, count == 1 ? null : name, truss);
        if (count == 1 && fixtures.Count == 1)
            fixtures[0].Name = name;
        if (group)
            Group(project, name, fixtures);
        state.Commit();
        return fixtures.Select(f => f.Id).ToList();
    }

    /// <summary>
    /// Adds fixtures like <see cref="Patch"/> but without committing, so a caller can make them part of a bigger
    /// edit. They're named "<paramref name="prefix"/> n" counting on from <paramref name="firstNumber"/>.
    /// </summary>
    /// <returns>The fixtures added, in patch order; may be fewer than <paramref name="count"/> when the universes run out.</returns>
    public static List<Fixture> Add(Project project, FixtureProfile profile, int count, int universe, int? startAddress, string? prefix, StageObject? truss, int firstNumber = 1)
    {
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
                Name = $"{prefix ?? profile.Model} {firstNumber + i}",
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
        return fixtures;
    }

    /// <summary>Puts <paramref name="fixtures"/> in a new group called <paramref name="name"/>; does nothing for fewer than two.</summary>
    public static void Group(Project project, string name, IReadOnlyCollection<Fixture> fixtures)
    {
        if (fixtures.Count < 2)
            return;
        var created = new FixtureGroup { Name = name, Color = GroupPalette.ForIndex(project.Groups.Count) };
        created.FixtureIds.AddRange(fixtures.Select(f => f.Id));
        project.Groups.Add(created);
    }
}
