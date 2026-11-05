using Lampion.Core.Engine;
using Lampion.Core.Model;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Controls.Timeline;

/// <summary>
/// How many fixtures a track drives, counted the way <see cref="TrackTargets.Resolve"/> resolves them
/// (explicit fixtures, then group members, unknown ids skipped, duplicates once) but without allocating.
/// The colour-strip preview only needs the count. UI thread only.
/// </summary>
internal sealed class TrackTargetCounter
{
    private readonly HashSet<string> _known = new(StringComparer.Ordinal);
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

    /// <summary>Indexes the project's fixture ids; call before a batch of <see cref="Count"/> calls whenever the patch may have changed.</summary>
    public void Begin(Project project)
    {
        _known.Clear();
        foreach (var fixture in project.Fixtures)
            if (fixture.Id is { } id)
                _known.Add(id);
    }

    /// <summary>The number of distinct patched fixtures <paramref name="track"/> drives, as of the last <see cref="Begin"/>.</summary>
    public int Count(Project project, Track track)
    {
        _seen.Clear();
        var count = 0;
        foreach (var id in track.FixtureIds)
            count += Visit(id);
        foreach (var groupId in track.GroupIds)
        {
            if (groupId is null)
                continue;
            foreach (var group in project.Groups)
            {
                if (group.Id != groupId)
                    continue;
                foreach (var id in group.FixtureIds)
                    count += Visit(id);
                break;
            }
        }
        return count;
    }

    private int Visit(string? id) => id is not null && _known.Contains(id) && _seen.Add(id) ? 1 : 0;
}
