using System.Runtime.InteropServices;
using Lampion.App.Editor;
using Lampion.App.Rendering;
using Lampion.Core.Model;
using LibVLCSharp.Shared;
using VlcMedia = LibVLCSharp.Shared.Media;

namespace Lampion.App.Media;

/// <summary>
/// Plays the timeline's audio and video through LibVLC, slaved to the editor clock: players are started,
/// seeked and paused to follow the playhead, and nudged back when they drift. Video frames are decoded
/// into <see cref="Video"/>, which both the video monitor and LED screens in the 3D stage display.
/// </summary>
public sealed class MediaEngine : IDisposable
{
    private const double DriftTolerance = 0.12;
    private const uint MaxVideoWidth = 1280;

    private readonly LibVLC? _vlc;
    private readonly Dictionary<string, ClipPlayer> _players = [];

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

    public string? Error { get; }
    public bool Available => _vlc is not null;
    public VideoFrame Video { get; } = new();

    /// <summary>Length of a media file in seconds, or null when LibVLC cannot read it.</summary>
    public async Task<double?> ProbeDurationAsync(string path)
    {
        if (_vlc is null)
            return null;
        using var media = new VlcMedia(_vlc, path, FromType.FromPath);
        var status = await media.Parse(MediaParseOptions.ParseLocal, timeout: 5000);
        return status == MediaParsedStatus.Done && media.Duration > 0 ? media.Duration / 1000.0 : null;
    }

    /// <summary>Brings every player in line with the playhead. Called once per UI frame.</summary>
    public void Sync(EditorState state)
    {
        if (_vlc is null)
            return;
        var t = state.Playhead;
        var live = new HashSet<string>();
        foreach (var track in state.Project.Tracks)
        {
            if (track.Kind == TrackKind.Light)
                continue;
            foreach (var clip in track.Clips.OfType<MediaClip>())
            {
                live.Add(clip.Id);
                var inside = t >= clip.Start && t < clip.End;
                if (!inside && !_players.ContainsKey(clip.Id))
                    continue;
                if (!File.Exists(clip.Path))
                    continue;
                var player = Player(clip, track.Kind == TrackKind.Video);
                var expected = t - clip.Start + clip.MediaOffset;
                player.Follow(inside, state.IsPlaying, expected, track.Muted ? 0 : clip.Volume);
            }
        }
        foreach (var gone in _players.Keys.Where(id => !live.Contains(id)).ToList())
        {
            _players[gone].Dispose();
            _players.Remove(gone);
        }
    }

    private ClipPlayer Player(MediaClip clip, bool video)
    {
        if (_players.TryGetValue(clip.Id, out var player) && player.Path == clip.Path)
            return player;
        player?.Dispose();
        player = new ClipPlayer(_vlc!, clip.Path, video ? Video : null);
        _players[clip.Id] = player;
        return player;
    }

    public void StopAll()
    {
        foreach (var player in _players.Values)
            player.Follow(false, false, 0, 0);
    }

    public void Dispose()
    {
        foreach (var player in _players.Values)
            player.Dispose();
        _players.Clear();
        _vlc?.Dispose();
    }

    /// <summary>One clip's LibVLC player, optionally rendering frames into a shared buffer.</summary>
    private sealed class ClipPlayer : IDisposable
    {
        private readonly MediaPlayer _player;
        private readonly VlcMedia _media;
        private readonly VideoFrame? _target;
        private double _lastSeek = double.NaN;
        private bool _started;
        private bool _rolling;

        // Native callbacks must stay referenced for as long as LibVLC may call them.
        private readonly MediaPlayer.LibVLCVideoFormatCb? _format;
        private readonly MediaPlayer.LibVLCVideoCleanupCb? _cleanup;
        private readonly MediaPlayer.LibVLCVideoLockCb? _lock;
        private readonly MediaPlayer.LibVLCVideoDisplayCb? _display;
        private IntPtr _buffer;
        private uint _width;
        private uint _height;

        public ClipPlayer(LibVLC vlc, string path, VideoFrame? target)
        {
            Path = path;
            _target = target;
            _media = new VlcMedia(vlc, path, FromType.FromPath);
            _player = new MediaPlayer(_media);
            if (target is not null)
            {
                _format = Format;
                _cleanup = Cleanup;
                _lock = Lock;
                _display = Display;
                _player.SetVideoFormatCallbacks(_format, _cleanup);
                _player.SetVideoCallbacks(_lock, null, _display);
            }
            else
                _media.AddOption(":no-video");
        }

        public string Path { get; }

        public void Follow(bool inside, bool playing, double expected, double volume)
        {
            _player.Volume = (int)Math.Round(Math.Clamp(volume, 0, 2) * 100);
            if (!inside || !playing)
            {
                if (_rolling)
                {
                    _player.SetPause(true);
                    _rolling = false;
                }
                if (!inside)
                    return;
            }
            if (playing)
            {
                // VLC opens media asynchronously, so IsPlaying lags Play(); track what was asked for instead
                // and only correct drift once the player is really running.
                if (!_rolling)
                {
                    _rolling = true;
                    if (!_started)
                        _player.Play();
                    _started = true;
                    _player.SetPause(false);
                    Seek(expected);
                    return;
                }
                if (_player.IsPlaying && Math.Abs(_player.Time / 1000.0 - expected) > DriftTolerance)
                    Seek(expected);
                return;
            }
            // Paused or scrubbing: hold still, but keep video on the frame under the playhead.
            if (_target is not null && (double.IsNaN(_lastSeek) || Math.Abs(_lastSeek - expected) > 0.04))
            {
                if (!_started)
                {
                    _started = true;
                    _player.Play();
                    _player.SetPause(true);
                }
                Seek(expected);
            }
        }

        private void Seek(double seconds)
        {
            _lastSeek = seconds;
            _player.Time = (long)Math.Max(0, seconds * 1000);
        }

        private uint Format(ref IntPtr opaque, IntPtr chroma, ref uint width, ref uint height, ref uint pitches, ref uint lines)
        {
            // Ask VLC to scale big sources down; the preview never needs more than this.
            if (width > MaxVideoWidth)
            {
                height = (uint)Math.Max(2, height * MaxVideoWidth / width) & ~1u;
                width = MaxVideoWidth;
            }
            Marshal.Copy("RV32"u8.ToArray(), 0, chroma, 4);
            _width = width;
            _height = height;
            pitches = width * 4;
            lines = height;
            if (_buffer != IntPtr.Zero)
                Marshal.FreeHGlobal(_buffer);
            _buffer = Marshal.AllocHGlobal((int)(pitches * lines));
            return 1;
        }

        private void Cleanup(ref IntPtr opaque)
        {
            if (_buffer == IntPtr.Zero)
                return;
            Marshal.FreeHGlobal(_buffer);
            _buffer = IntPtr.Zero;
        }

        private IntPtr Lock(IntPtr opaque, IntPtr planes)
        {
            Marshal.WriteIntPtr(planes, _buffer);
            return IntPtr.Zero;
        }

        private void Display(IntPtr opaque, IntPtr picture)
        {
            var target = _target!;
            var size = (int)(_width * _height * 4);
            lock (target)
            {
                if (target.Pixels.Length != size)
                    target.Pixels = new byte[size];
                Marshal.Copy(_buffer, target.Pixels, 0, size);
                target.Width = (int)_width;
                target.Height = (int)_height;
                target.Version++;
            }
        }

        public void Dispose()
        {
            _player.Stop();
            _player.Dispose();
            _media.Dispose();
            Cleanup(ref _buffer);
        }
    }
}
