using Lampion.App.Editor;
using Lampion.App.Views.NewFixtures;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Stage;

namespace Lampion.App.Views.TrussBuilder;

/// <summary>Puts a truss structure and its lights into the project as a single undo step.</summary>
internal static class TrussRigger
{
    /// <summary>
    /// Adds the pieces, patches and hangs the lights, groups the lights, makes it all one <see cref="Structure"/>,
    /// commits and selects the first span.
    /// </summary>
    /// <returns>A one-line summary for the status bar, mentioning lights that didn't fit in the DMX universes.</returns>
    public static string Rig(EditorState state, TrussBuildSettings settings)
    {
        var project = state.Project;
        var structure = settings.Build();
        project.Stage.Objects.AddRange(structure.Pieces);

        var lights = new List<Fixture>();
        var wanted = 0;
        foreach (var span in settings.LitSpans(structure))
        {
            wanted += settings.LightsPerSpan;
            var universe = lights.Count > 0 ? lights[^1].Universe : 1;
            lights.AddRange(FixturePatcher.Add(project, settings.Profile!, settings.LightsPerSpan, universe, null, $"{settings.Name} light", span, lights.Count + 1));
        }
        FixturePatcher.Group(project, $"{settings.Name} lights", lights);
        StructureEdits.Create(project, settings.Name, structure.Pieces, lights, settings.Yaw);

        state.Commit();
        state.SelectObject(structure.Spans[0].Id);
        var summary = $"Built {settings.Name}: {structure.Pieces.Count} truss pieces";
        if (wanted == 0)
            return summary;
        summary += $", {lights.Count} lights";
        return lights.Count < wanted ? $"{summary} ({wanted - lights.Count} didn't fit: universes 1–64 are full)" : summary;
    }
}
