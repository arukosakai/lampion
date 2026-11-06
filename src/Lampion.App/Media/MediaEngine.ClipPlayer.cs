using System.Diagnostics;
using System.Runtime.InteropServices;
using Lampion.App.Rendering;
using LibVLCSharp.Shared;
using VlcMedia = LibVLCSharp.Shared.Media;

namespace Lampion.App.Media;

internal sealed partial class MediaEngine
{
    /// <summary>One clip's LibVLC player, optionally rendering frames into a shared buffer.</summary>
    private sealed class ClipPlayer : IDisposable
    {
        // Paused video re-seeks only when the playhead moved more than this many seconds (about a frame).
        private const double ScrubTolerance = 0.04;

        // Time updates seen since the last seek before drift is trusted; the first ones still reflect buffering.
        private const int SettleReports = 3;

        // Upper bound on the learned seek lead, so one bad measurement can't throw playback far ahead.
        private const double MaxSeekLead = 1.0;

        private static readonly byte[] Rv32 = "RV32"u8.ToArray();

        private readonly MediaPlayer _player;
        private readonly VlcMedia _media;
        private readonly VideoFrame? _target;
        private double _lastSeek = double.NaN;
        private bool _started;
        private bool _rolling;

        private long _reportedMs = long.MinValue;
        private long _reportedAt;
        private int _reports;
        private double _seekLead;
        private bool _measureLead;

        // Native callbacks must stay referenced for as long as LibVLC may call them.
        private readonly MediaPlayer.LibVLCVideoFormatCb? _format;
        private readonly MediaPlayer.LibVLCVideoCleanupCb? _cleanup;
        private readonly MediaPlayer.LibVLCVideoLockCb? _lock;
        private readonly MediaPlayer.LibVLCVideoDisplayCb? _display;
        private IntPtr _buffer;
        private uint _width;
        private uint _height;

        /// <summary>Opens <paramref name="path"/>; frames are decoded into <paramref name="target"/>, or audio only when it is null.</summary>
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

        /// <summary>The media file this player has open.</summary>
        public string Path { get; }

        /// <summary>The <see cref="MediaEngine.Sync"/> pass that last found this player's clip.</summary>
        public int Seen { get; set; }

        /// <summary>Plays, pauses or seeks to follow the playhead. Allocation-free.</summary>
        /// <param name="inside">True when the playhead is within the clip.</param>
        /// <param name="playing">True while the editor is playing.</param>
        /// <param name="expected">Where in the media file the playhead is, in seconds.</param>
        /// <param name="volume">Gain from 0 to 2; 1 is unchanged.</param>
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
                    SeekWhilePlaying(expected, learnLead: false);
                    return;
                }
                CorrectDrift(expected);
                return;
            }
            // Paused or scrubbing: hold still, but keep video on the frame under the playhead.
            if (_target is not null && (double.IsNaN(_lastSeek) || Math.Abs(_lastSeek - expected) > ScrubTolerance))
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

        private void CorrectDrift(double expected)
        {
            var position = EstimatePosition();
            if (!_player.IsPlaying || _reports < SettleReports)
                return;
            var behind = expected - position;
            // A seek resumes late by VLC's start-up latency; the first settled reading after a correction measures it.
            if (_measureLead)
            {
                _measureLead = false;
                _seekLead = Math.Clamp(_seekLead + behind, 0, MaxSeekLead);
            }
            if (Math.Abs(behind) > DriftTolerance)
                SeekWhilePlaying(expected, learnLead: true);
        }

        // LibVLC refreshes Time only every ~250 ms, so extrapolate from when the reported value last changed.
        private double EstimatePosition()
        {
            var now = Stopwatch.GetTimestamp();
            var ms = _player.Time;
            if (ms != _reportedMs)
            {
                _reportedMs = ms;
                _reportedAt = now;
                _reports++;
            }
            return ms / 1000.0 + Stopwatch.GetElapsedTime(_reportedAt, now).TotalSeconds;
        }

        // Opening or unpausing is far slower than seeking a running player, so only corrections teach the lead.
        private void SeekWhilePlaying(double expected, bool learnLead)
        {
            Seek(expected + _seekLead);
            _reportedMs = long.MinValue;
            _reports = 0;
            _measureLead = learnLead;
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
            Marshal.Copy(Rv32, 0, chroma, Rv32.Length);
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

        // Runs on LibVLC's decode thread; the frame buffer is only reallocated when the video size changes.
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

        /// <summary>Stops playback and frees the player and its frame buffer.</summary>
        public void Dispose()
        {
            _player.Stop();
            _player.Dispose();
            _media.Dispose();
            Cleanup(ref _buffer);
        }
    }
}
