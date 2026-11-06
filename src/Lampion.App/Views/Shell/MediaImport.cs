using Avalonia.Controls;
using Lampion.App.Editor;
using Lampion.App.Media;
using Lampion.Core.Media;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Views.Shell;

/// <summary>
/// Puts audio and video files on the timeline at the playhead as one undo step, from File → Import audio / video
/// or from files dropped onto the window.
/// </summary>
internal sealed class MediaImport
{
    // Used when neither LibVLC nor the waveform reader can tell how long the file is.
    private const double FallbackDuration = 60;

    private static readonly string[] AudioPatterns = ["*.mp3", "*.wav", "*.ogg", "*.flac", "*.m4a", "*.aac"];
    private static readonly string[] VideoPatterns = ["*.mp4", "*.mov", "*.mkv", "*.webm", "*.avi", "*.m4v"];

    private readonly Window _owner;
    private readonly EditorState _state;
    private readonly MediaEngine _media;
    private readonly StatusBar _status;
    private readonly Action _imported;

    /// <summary>Imports into <paramref name="state"/>.</summary>
    /// <param name="owner">Owner of the file picker.</param>
    /// <param name="state">The editor state.</param>
    /// <param name="media">Probes media length.</param>
    /// <param name="status">Where problems are reported.</param>
    /// <param name="imported">Runs after clips were added (e.g. to zoom the timeline to fit).</param>
    public MediaImport(Window owner, EditorState state, MediaEngine media, StatusBar status, Action imported)
    {
        _owner = owner;
        _state = state;
        _media = media;
        _status = status;
        _imported = imported;
    }

    /// <summary>Whether a file of this name would be imported as audio or video, by its extension; null when neither.</summary>
    public static TrackKind? KindOf(string path)
    {
        var pattern = "*" + Path.GetExtension(path).ToLowerInvariant();
        if (AudioPatterns.Contains(pattern))
            return TrackKind.Audio;
        if (VideoPatterns.Contains(pattern))
            return TrackKind.Video;
        return null;
    }

    /// <summary>Asks for an audio or video file and adds it as a clip on the first track of that kind.</summary>
    public async Task Import(TrackKind kind)
    {
        var patterns = kind == TrackKind.Audio ? AudioPatterns : VideoPatterns;
        var path = await Dialogs.PickOpen(_owner, $"Import {kind.ToString().ToLowerInvariant()}", (kind.ToString(), patterns));
        if (path is not null)
            await Import([path]);
    }

    /// <summary>
    /// Adds every audio and video file in <paramref name="paths"/> from the playhead, each kind back to back on its
    /// own track, as one undo step. Other files are skipped and named in the status bar.
    /// </summary>
    public async Task Import(IReadOnlyList<string> paths)
    {
        var audioStart = _state.Playhead;
        var videoStart = _state.Playhead;
        var skipped = new List<string>();
        var unreadable = new List<string>();
        MediaClip? last = null;
        foreach (var path in paths)
        {
            if (KindOf(path) is not { } kind)
            {
                skipped.Add(Path.GetFileName(path));
                continue;
            }
            var start = kind == TrackKind.Audio ? audioStart : videoStart;
            var clip = await AddClip(path, kind, start);
            if (clip.SourceDuration is null)
                unreadable.Add(Path.GetFileName(path));
            if (kind == TrackKind.Audio)
                audioStart = clip.End;
            else
                videoStart = clip.End;
            last = clip;
        }
        if (last is not null)
        {
            _state.Commit();
            _state.SelectClip(last.Id);
            _imported();
        }
        if (Problems(skipped, unreadable) is { } problems)
            _status.Message = problems;
    }

    private async Task<MediaClip> AddClip(string path, TrackKind kind, double start)
    {
        var peaks = kind == TrackKind.Audio ? await Task.Run(() => WaveformReader.TryRead(path)) : null;
        var duration = await _media.ProbeDurationAsync(path) ?? peaks?.Duration;
        var track = TimelineOps.EnsureMediaTrack(_state, kind);
        var clip = new MediaClip
        {
            Name = Path.GetFileNameWithoutExtension(path),
            Path = path,
            Start = start,
            Duration = duration ?? FallbackDuration,
            SourceDuration = duration,
        };
        _state.Waveforms[path] = peaks;
        track.Clips.Add(clip);
        track.SortClips();
        // The first song usually defines the show; stretch the show to fit it.
        if (clip.End > _state.Project.Duration)
            _state.Project.Duration = Math.Ceiling(clip.End);
        return clip;
    }

    private static string? Problems(List<string> skipped, List<string> unreadable)
    {
        var parts = new List<string>();
        if (skipped.Count > 0)
            parts.Add($"Skipped {string.Join(", ", skipped)}: only audio ({Extensions(AudioPatterns)}) and video ({Extensions(VideoPatterns)}) can be imported.");
        if (unreadable.Count > 0)
            parts.Add($"Could not read the length of {string.Join(", ", unreadable)}; set the clip duration in Effect Controls.");
        return parts.Count == 0 ? null : string.Join(" ", parts);
    }

    private static string Extensions(string[] patterns) => string.Join(" ", patterns.Select(p => p[2..]));
}
