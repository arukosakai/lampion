using System.Diagnostics;
using System.Globalization;
using Lampion.Core.Model.Timeline;
using LibVLCSharp.Shared;
using VlcMedia = LibVLCSharp.Shared.Media;

namespace Lampion.App.Live;

internal sealed partial class LiveMedia
{
    /// <summary>One audio clip's player. Opened at the right position (paused for a preroll) and closed again when the clip ends.</summary>
    private sealed class Voice(LibVLC vlc, MediaClip clip, LiveMedia owner) : IDisposable
    {
        private MediaPlayer? _player;
        private VlcMedia? _media;
        private long _startMs;
        private volatile bool _paused;
        private volatile bool _ready;

        /// <summary>The clip it plays.</summary>
        public MediaClip Clip { get; } = clip;

        /// <summary>True once a clip opened paused has buffered, so releasing it starts the sound with little delay.</summary>
        public bool Ready => _ready;

        /// <summary>True from <see cref="Release"/> (or an unpaused <see cref="Prepare"/>) until <see cref="Stop"/>.</summary>
        public bool IsPlaying => _player is not null && !_paused;

        /// <summary>Opens the clip at <paramref name="position"/> seconds into the file, paused or playing; does nothing if already open.</summary>
        public void Prepare(double position, bool paused)
        {
            if (_player is not null)
                return;
            // Start position and pause are media options, so VLC opens the file there instead of seeking after opening.
            _media = new VlcMedia(vlc, Clip.Path, FromType.FromPath);
            _media.AddOption(":start-time=" + position.ToString("0.000", CultureInfo.InvariantCulture));
            if (paused)
                _media.AddOption(":start-paused");
            _startMs = (long)(position * 1000);
            _ready = false;
            _paused = paused;
            _player = new MediaPlayer(_media);
            _player.TimeChanged += OnTime;
            _player.Paused += OnPaused;
            _player.Play();
            _player.Volume = (int)Math.Round(Math.Clamp(Clip.Volume, 0, 2) * 100);
        }

        /// <summary>Unpauses a prerolled clip.</summary>
        public void Release()
        {
            if (_player is null || !_paused)
                return;
            _paused = false;
            _player.SetPause(false);
        }

        /// <summary>Closes the player; the clip can be prepared again (a loop).</summary>
        public void Stop()
        {
            if (_player is null)
                return;
            _player.TimeChanged -= OnTime;
            _player.Paused -= OnPaused;
            _player.Stop();
            _player.Dispose();
            _media?.Dispose();
            _player = null;
            _media = null;
        }

        /// <summary>Closes the player.</summary>
        public void Dispose() => Stop();

        // LibVLC's event thread: a clip opened paused has finished buffering and will sound almost at once when released.
        private void OnPaused(object? sender, EventArgs e) => _ready = true;

        // Guarded: an exception on LibVLC's event thread would end the process mid-show.
        private static void Safely(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Trace.TraceError("Live audio callback failed: " + ex);
            }
        }

        // LibVLC's event thread. Reports before the position moves are VLC opening the file, not sound coming out.
        private void OnTime(object? sender, MediaPlayerTimeChangedEventArgs e)
        {
            var timestamp = Stopwatch.GetTimestamp();
            var ms = e.Time;
            if (!_paused && ms > _startMs)
                Safely(() => owner.OnPosition(this, ms / 1000.0, timestamp));
        }
    }
}
