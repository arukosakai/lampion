using Lampion.Core.Model;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Views.ProjectBin;

/// <summary>What dragging the bin selection onto the timeline lights, and what the new track is called.</summary>
/// <param name="Fixtures">Selected fixture ids, in selection order.</param>
/// <param name="Groups">Selected group ids, in selection order.</param>
/// <param name="Name">Track name: the group's or fixture's when exactly one is dragged, else "Lights N".</param>
internal readonly record struct BinDragPayload(List<string> Fixtures, List<string> Groups, string Name)
{
    /// <summary>True when nothing draggable (no fixture or group) is selected.</summary>
    public bool IsEmpty => Fixtures.Count + Groups.Count == 0;

    /// <summary>The payload for <paramref name="items"/>; folders, profiles and media are left out.</summary>
    public static BinDragPayload Of(IReadOnlyList<BinItem> items, Project project)
    {
        var groups = items.Where(i => i.Kind == BinItemKind.Group).Select(i => i.Id).ToList();
        var fixtures = items.Where(i => i.Kind == BinItemKind.Fixture).Select(i => i.Id).ToList();
        var name = groups.Count == 1 && fixtures.Count == 0 && project.Groups.Find(g => g.Id == groups[0]) is { } group
            ? group.Name
            : fixtures.Count == 1 && groups.Count == 0 && project.FixtureById(fixtures[0]) is { } fixture
                ? fixture.Name
                : $"Lights {project.Tracks.Count(t => t.Kind == TrackKind.Light) + 1}";
        return new BinDragPayload(fixtures, groups, name);
    }
}
