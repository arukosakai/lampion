using Lampion.Core.Model;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Timeline;

namespace Lampion.Core.Engine;

/// <summary>One-off target resolution for UI code. The evaluator resolves targets itself without allocating.</summary>
public static class TrackTargets
{
    /// <summary>
    /// The track's fixtures (explicit ones first, then group members), deduplicated and in the order its
    /// effects walk them, exactly as the evaluator sees them. Allocates a new list each call.
    /// </summary>
    public static List<Fixture> Resolve(Project project, Track track)
    {
        var index = new ProjectIndex(project);
        index.Rebuild();
        var targets = new List<int>();
        new TargetResolver().Resolve(index, track, targets);
        var fixtures = new List<Fixture>(targets.Count);
        foreach (var target in targets)
            fixtures.Add(project.Fixtures[target]);
        return fixtures;
    }
}
