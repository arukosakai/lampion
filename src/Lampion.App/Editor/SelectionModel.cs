using Lampion.Core.Model.Animation;

namespace Lampion.App.Editor;

/// <summary>
/// What is selected: clips, a track, fixtures, a set piece or a keyframe. Holds ids only, so it survives
/// the project instance being replaced; <see cref="Prune"/> drops the ids that no longer resolve.
/// Raises no events; <see cref="EditorState"/> does.
/// </summary>
internal sealed class SelectionModel
{
    /// <summary>Which of the id sets is the selection that counts.</summary>
    public SelectionKind Kind { get; private set; }

    /// <summary>The selected track, or the track of the last selected clip. Kept when the rest is cleared.</summary>
    public string? TrackId { get; private set; }

    /// <summary>Selected clip ids. Mutable so drag-selection can edit it in place.</summary>
    public HashSet<string> ClipIds { get; } = [];

    /// <summary>Selected fixture ids. Mutable so drag-selection can edit it in place.</summary>
    public HashSet<string> FixtureIds { get; } = [];

    /// <summary>The selected stage set piece, or null.</summary>
    public string? ObjectId { get; private set; }

    /// <summary>The keyframe the timeline last picked, or null.</summary>
    public (string LaneId, Keyframe Key)? Key { get; set; }

    /// <summary>Any one selected clip id (the set is unordered), or null.</summary>
    public string? AnyClipId => First(ClipIds);

    /// <summary>Any one selected fixture id (the set is unordered), or null.</summary>
    public string? AnyFixtureId => First(FixtureIds);

    /// <summary>Selects nothing, except that <see cref="TrackId"/> is kept for whoever needs a current track.</summary>
    public void Clear()
    {
        Kind = SelectionKind.None;
        ClipIds.Clear();
        FixtureIds.Clear();
        ObjectId = null;
        Key = null;
    }

    /// <summary>Selects a clip, added to the current clips when <paramref name="additive"/>.</summary>
    /// <param name="clipId">The clip to select.</param>
    /// <param name="trackId">The track holding it, or null to keep <see cref="TrackId"/>.</param>
    /// <param name="additive">True to keep the clips already selected.</param>
    public void SelectClip(string clipId, string? trackId, bool additive)
    {
        if (!additive)
            Clear();
        ClipIds.Add(clipId);
        Kind = SelectionKind.Clip;
        if (trackId is not null)
            TrackId = trackId;
    }

    /// <summary>Selects one track and nothing else.</summary>
    public void SelectTrack(string trackId)
    {
        Clear();
        TrackId = trackId;
        Kind = SelectionKind.Track;
    }

    /// <summary>Selects fixtures; when <paramref name="additive"/>, ids already selected are toggled off instead.</summary>
    public void SelectFixtures(IEnumerable<string> ids, bool additive)
    {
        if (!additive)
            Clear();
        foreach (var id in ids)
            if (!FixtureIds.Add(id) && additive)
                FixtureIds.Remove(id);
        Kind = FixtureIds.Count > 0 ? SelectionKind.Fixture : SelectionKind.None;
    }

    /// <summary>Selects one stage set piece, or nothing for null.</summary>
    public void SelectObject(string? id)
    {
        Clear();
        ObjectId = id;
        Kind = id is null ? SelectionKind.None : SelectionKind.StageObject;
    }

    /// <summary>Drops ids that no longer exist in <paramref name="document"/> and the picked keyframe, then fixes up <see cref="Kind"/>.</summary>
    public void Prune(ProjectDocument document)
    {
        ClipIds.RemoveWhere(id => document.FindClip(id) is null);
        FixtureIds.RemoveWhere(id => document.Project.FixtureById(id) is null);
        if (document.StageObjectById(ObjectId) is null)
            ObjectId = null;
        if (document.TrackById(TrackId) is null)
            TrackId = null;
        Key = null;
        Kind = Kind switch
        {
            SelectionKind.Clip when ClipIds.Count == 0 => SelectionKind.None,
            SelectionKind.Fixture when FixtureIds.Count == 0 => SelectionKind.None,
            SelectionKind.StageObject when ObjectId is null => SelectionKind.None,
            SelectionKind.Track when TrackId is null => SelectionKind.None,
            var kind => kind,
        };
    }

    private static string? First(HashSet<string> ids)
    {
        foreach (var id in ids)
            return id;
        return null;
    }
}
