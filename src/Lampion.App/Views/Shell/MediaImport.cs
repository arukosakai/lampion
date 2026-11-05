using Avalonia.Controls;
using Lampion.App.Editor;
using Lampion.App.Media;
using Lampion.Core.Media;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Views.Shell;

/// <summary>File → Import audio / video: picks a file and lays it on the timeline at the playhead as one undo step.</summary>
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
    /// <param name="imported">Runs after a clip was added (e.g. to zoom the timeline to fit).</param>
    public MediaImport(Window owner, EditorState state, MediaEngine media, StatusBar status, Action imported)
    {
        _owner = owner;
        _state = state;
        _media = media;
        _status = status;
        _imported = imported;
    }

    /// <summary>Asks for an audio or video file and adds it as a clip on the first track of that kind.</summary>
    public async Task Import(TrackKind kind)
    {
        var patterns = kind == TrackKind.Audio ? AudioPatterns : VideoPatterns;
        var path = await Dialogs.PickOpen(_owner, $"Import {kind.ToString().ToLowerInvariant()}", (kind.ToString(), patterns));
        if (path is null)
            return;
        var peaks = kind == TrackKind.Audio ? await Task.Run(() => WaveformReader.TryRead(path)) : null;
        var duration = await _media.ProbeDurationAsync(path) ?? peaks?.Duration;
        var track = TimelineOps.EnsureMediaTrack(_state, kind);
        var clip = new MediaClip
        {
            Name = Path.GetFileNameWithoutExtension(path),
            Path = path,
            Start = _state.Playhead,
            Duration = duration ?? FallbackDuration,
            SourceDuration = duration,
        };
        _state.Waveforms[path] = peaks;
        track.Clips.Add(clip);
        track.SortClips();
        // The first song usually defines the show; stretch the show to fit it.
        if (clip.End > _state.Project.Duration)
            _state.Project.Duration = Math.Ceiling(clip.End);
        _state.Commit();
        _state.SelectClip(clip.Id);
        if (duration is null)
            _status.Message = $"Could not read the length of {Path.GetFileName(path)}; set the clip duration in Effect Controls.";
        _imported();
    }
}
