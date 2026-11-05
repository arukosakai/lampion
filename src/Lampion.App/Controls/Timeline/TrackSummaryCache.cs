using Lampion.Core.Model;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Controls.Timeline;

/// <summary>
/// The "what does this light track drive" line under each track name ("Wash, 3 fixtures · Add"). The text
/// is rebuilt only when its inputs change: group names (by reference), fixture count or blend mode.
/// UI thread only.
/// </summary>
internal sealed partial class TrackSummaryCache
{
    private const string NoTargets = "no fixtures — pick some in Inspector";

    private readonly Dictionary<Track, Entry> _entries = [];
    private readonly List<string> _names = [];

    /// <summary>The summary of <paramref name="track"/>; allocation-free while nothing it shows has changed.</summary>
    public string Get(Project project, Track track)
    {
        _names.Clear();
        foreach (var groupId in track.GroupIds)
            if (GroupName(project, groupId) is { } name)
                _names.Add(name);

        if (!_entries.TryGetValue(track, out var entry))
        {
            entry = new Entry();
            _entries[track] = entry;
        }
        if (entry.Text is null || entry.FixtureCount != track.FixtureIds.Count || entry.Blend != track.Blend || !entry.SameNames(_names))
            entry.Update(Build(track), track, _names);
        return entry.Text!;
    }

    /// <summary>Drops cached entries once there are many more than live tracks (deleted tracks, replaced projects).</summary>
    public void Prune(int liveTracks)
    {
        if (_entries.Count > liveTracks * 2 + 16)
            _entries.Clear();
    }

    private static string? GroupName(Project project, string id)
    {
        foreach (var group in project.Groups)
            if (group.Id == id)
                return group.Name;
        return null;
    }

    private string Build(Track track)
    {
        var parts = new List<string>(_names);
        if (track.FixtureIds.Count > 0)
            parts.Add($"{track.FixtureIds.Count} fixture{(track.FixtureIds.Count == 1 ? "" : "s")}");
        var text = parts.Count == 0 ? NoTargets : string.Join(", ", parts);
        if (track.Blend != BlendMode.Normal)
            text += $" · {track.Blend}";
        return text;
    }
}
