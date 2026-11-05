using Lampion.App.Editor;
using Lampion.App.Rendering;
using Lampion.Core.Model.Timeline;
using LibVLCSharp.Shared;
using VlcMedia = LibVLCSharp.Shared.Media;

namespace Lampion.App.Media;

/// <summary>
/// Plays the timeline's audio and video through LibVLC, slaved to the editor clock: players are started,
/// seeked and paused to follow the playhead, and nudged back when they drift. Video frames are decoded
/// into <see cref="Video"/>, which both the video monitor and LED screens in the 3D stage display.
/// UI thread only, apart from the decode callbacks that fill <see cref="Video"/>.
/// </summary>
internal sealed partial class MediaEngine : IDisposable
{
    // Seconds a playing clip may drift from the playhead before it is seeked back.
    private const double DriftTolerance = 0.12;
    private const uint MaxVideoWidth = 1280;

    private readonly LibVLC? _vlc;
    private readonly Dictionary<string, ClipPlayer> _players = [];
    private readonly List<string> _gone = [];
    private int _generation;

    /// <summary>Starts LibVLC; on failure the engine stays usable but silent, and <see cref="Error"/> says why.</summary>
    public MediaEngine()
    {
        try
        {
            LibVLCSharp.Shared.Core.Initialize();
            _vlc = new LibVLC("--quiet", "--no-video-title-show", "--no-osd");
        }
        catch (Exception ex)
        {
            Error = $"Media playback unavailable (LibVLC): {ex.Message}";
        }
    }

    /// <summary>Why media playback is unavailable, in words for the status bar; null when it works.</summary>
    public string? Error { get; }

    /// <summary>True when LibVLC loaded and media can play.</summary>
    public bool Available => _vlc is not null;

    /// <summary>The latest decoded video frame, written from LibVLC's decode thread under a lock on itself.</summary>
    public VideoFrame Video { get; } = new();

    /// <summary>Length of a media file in seconds, or null when LibVLC cannot read it (or is unavailable).</summary>
    public async Task<double?> ProbeDurationAsync(string path)
    {
        if (_vlc is null)
            return null;
        using var media = new VlcMedia(_vlc, path, FromType.FromPath);
        var status = await media.Parse(MediaParseOptions.ParseLocal, timeout: 5000);
        return status == MediaParsedStatus.Done && media.Duration > 0 ? media.Duration / 1000.0 : null;
    }

    /// <summary>
    /// Brings every player in line with the playhead, opening players for clips under it and closing those
    /// whose clip is gone. Called once per UI frame; allocation-free unless a player opens or closes.
    /// </summary>
    public void Sync(EditorState state)
    {
        if (_vlc is null)
            return;
        var t = state.Playhead;
        var playing = state.IsPlaying;
        _generation++;
        foreach (var track in state.Project.Tracks)
        {
            if (track.Kind == TrackKind.Light)
                continue;
            foreach (var item in track.Clips)
            {
                if (item is not MediaClip clip)
                    continue;
                _players.TryGetValue(clip.Id, out var existing);
                if (existing is not null)
                    existing.Seen = _generation;
                var inside = t >= clip.Start && t < clip.End;
                if (!inside && existing is null)
                    continue;
                var player = existing is not null && existing.Path == clip.Path ? existing : TryOpen(clip, track.Kind == TrackKind.Video, existing);
                player?.Follow(inside, playing, t - clip.Start + clip.MediaOffset, track.Muted ? 0 : clip.Volume);
            }
        }
        CloseUnseen();
    }

    /// <summary>Holds every player still, e.g. before the project is replaced.</summary>
    public void StopAll()
    {
        foreach (var player in _players.Values)
            player.Follow(false, false, 0, 0);
    }

    /// <summary>Stops and releases every player and LibVLC itself.</summary>
    public void Dispose()
    {
        foreach (var player in _players.Values)
            player.Dispose();
        _players.Clear();
        _vlc?.Dispose();
    }

    // Opens a player for the clip's file, replacing one opened for an earlier path. A missing file opens
    // nothing and leaves any existing player alone, as if the clip were not there.
    private ClipPlayer? TryOpen(MediaClip clip, bool video, ClipPlayer? existing)
    {
        if (!File.Exists(clip.Path))
            return null;
        existing?.Dispose();
        var player = new ClipPlayer(_vlc!, clip.Path, video ? Video : null) { Seen = _generation };
        _players[clip.Id] = player;
        return player;
    }

    private void CloseUnseen()
    {
        foreach (var (id, player) in _players)
            if (player.Seen != _generation)
                _gone.Add(id);
        if (_gone.Count == 0)
            return;
        foreach (var id in _gone)
        {
            _players[id].Dispose();
            _players.Remove(id);
        }
        _gone.Clear();
    }
}
