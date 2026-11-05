using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Editor;

/// <summary>Undoable edits to the timeline's tracks: adding, reordering, deleting, retargeting and the header toggles.</summary>
internal static class TrackEdits
{
    /// <summary>
    /// Adds a light track named "Lights n", commits and selects it. Without <paramref name="index"/> it goes
    /// above the first media track, so light tracks stay together at the top.
    /// </summary>
    /// <param name="state">The editor.</param>
    /// <param name="fixtureIds">Fixtures it drives, or null for none.</param>
    /// <param name="groupIds">Groups it drives, or null for none.</param>
    /// <param name="index">Position in <c>Project.Tracks</c>, or null for the default spot.</param>
    /// <returns>The new track.</returns>
    public static Track AddLightTrack(EditorState state, IEnumerable<string>? fixtureIds = null, IEnumerable<string>? groupIds = null, int? index = null)
    {
        var tracks = state.Project.Tracks;
        var count = tracks.Count(t => t.Kind == TrackKind.Light);
        var track = new Track
        {
            Name = $"Lights {count + 1}",
            Order = FixtureOrder.LeftToRight,
        };
        if (fixtureIds is not null)
            track.FixtureIds.AddRange(fixtureIds);
        if (groupIds is not null)
            track.GroupIds.AddRange(groupIds);
        var at = index ?? tracks.FindIndex(t => t.Kind != TrackKind.Light);
        tracks.Insert(at < 0 ? tracks.Count : at, track);
        state.Commit();
        state.SelectTrack(track.Id);
        return track;
    }

    /// <summary>The first track of <paramref name="kind"/>, appending a new one if there is none. Doesn't commit.</summary>
    public static Track EnsureMediaTrack(EditorState state, TrackKind kind)
    {
        var track = state.Project.Tracks.Find(t => t.Kind == kind);
        if (track is not null)
            return track;
        track = new Track { Name = kind.ToString(), Kind = kind };
        state.Project.Tracks.Add(track);
        return track;
    }

    /// <summary>Adds fixtures and groups the light track doesn't drive yet, commits and selects the track.</summary>
    public static void AddTargets(EditorState state, Track track, IEnumerable<string> fixtureIds, IEnumerable<string> groupIds)
    {
        track.FixtureIds.AddRange(fixtureIds.Except(track.FixtureIds));
        track.GroupIds.AddRange(groupIds.Except(track.GroupIds));
        state.Commit();
        state.SelectTrack(track.Id);
    }

    /// <summary>Moves <paramref name="track"/> one place up (-1) or down (+1); does nothing at either end.</summary>
    public static void Move(EditorState state, Track track, int direction)
    {
        var tracks = state.Project.Tracks;
        var i = tracks.IndexOf(track);
        var j = Math.Clamp(i + direction, 0, tracks.Count - 1);
        if (i == j)
            return;
        tracks.RemoveAt(i);
        tracks.Insert(j, track);
        state.Commit();
    }

    /// <summary>Removes <paramref name="track"/> and its clips, commits and clears the selection.</summary>
    public static void Delete(EditorState state, Track track)
    {
        state.Project.Tracks.Remove(track);
        state.Commit();
        state.Deselect();
    }

    /// <summary>Flips the mute toggle and commits.</summary>
    public static void ToggleMute(EditorState state, Track track)
    {
        track.Muted = !track.Muted;
        state.Commit();
    }

    /// <summary>Flips the solo toggle and commits.</summary>
    public static void ToggleSolo(EditorState state, Track track)
    {
        track.Solo = !track.Solo;
        state.Commit();
    }

    /// <summary>Flips the lock and commits.</summary>
    public static void ToggleLock(EditorState state, Track track)
    {
        track.Locked = !track.Locked;
        state.Commit();
    }

    /// <summary>Opens or closes the twirl-down. A view change, so it touches rather than adding an undo step.</summary>
    public static void ToggleExpanded(EditorState state, Track track)
    {
        track.Expanded = !track.Expanded;
        state.Touch();
    }
}
