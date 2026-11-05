using System.Text.Json;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Editor;

/// <summary>Copy and paste of a single clip within the app (not the system clipboard). UI thread only.</summary>
internal static class ClipClipboard
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static string? _clip;

    /// <summary>Remembers the primary selected clip; does nothing when no clip is selected.</summary>
    public static void Copy(EditorState state)
    {
        if (state.PrimaryClip is { } clip)
            _clip = JsonSerializer.Serialize<Clip>(clip, Json);
    }

    /// <summary>
    /// Pastes a fresh copy of the copied clip at the playhead, on the selected track when it is the right kind,
    /// else the first track that is. Commits and selects it; does nothing if there's nothing copied or no track fits.
    /// </summary>
    public static void Paste(EditorState state)
    {
        if (_clip is null)
            return;
        var clip = JsonSerializer.Deserialize<Clip>(_clip, Json)!.Clone();
        var kind = clip is LightClip
            ? TrackKind.Light
            : state.Project.Tracks.FirstOrDefault(t => t.Clips.Any(c => c is MediaClip m && m.Path == ((MediaClip)clip).Path))?.Kind ?? TrackKind.Audio;
        var track = state.SelectedTrack is { } selected && selected.Kind == kind ? selected : state.Project.Tracks.FirstOrDefault(t => t.Kind == kind);
        if (track is null)
            return;
        clip.Start = state.Playhead;
        track.Clips.Add(clip);
        track.SortClips();
        state.Commit();
        state.SelectClip(clip.Id);
    }
}
