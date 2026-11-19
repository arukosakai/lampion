using Lampion.App.Media;
using LibVLCSharp.Shared;
using VlcMedia = LibVLCSharp.Shared.Media;

namespace Lampion.App.Live;

/// <summary>One play of a pad's sound file: opened (paused, or already playing), played once, then disposed. UI thread only.</summary>
internal sealed class PadShot : IDisposable
{
    private readonly VlcMedia _media;
    private readonly MediaPlayer _player;
    private volatile bool _ready;
    private volatile bool _ended;

    /// <summary>Opens <paramref name="path"/> from its start; <paramref name="paused"/> holds it buffered until <see cref="Play"/>.</summary>
    public PadShot(LibVLC vlc, string path, bool paused)
    {
        _media = new VlcMedia(vlc, AudioProxies.Cache.Resolve(path), FromType.FromPath);
        if (paused)
            _media.AddOption(":start-paused");
        _player = new MediaPlayer(_media);
        _player.Paused += OnPaused;
        _player.EndReached += OnEnded;
        _player.Play();
    }

    /// <summary>True once a shot opened paused has buffered, so <see cref="Play"/> sounds almost at once.</summary>
    public bool Ready => _ready;

    /// <summary>True once the file has played to its end.</summary>
    public bool Ended => _ended;

    /// <summary>Sets the volume (0..2, 1 = as recorded) and unpauses.</summary>
    public void Play(double volume)
    {
        _player.Volume = (int)Math.Round(Math.Clamp(volume, 0, 2) * 100);
        _player.SetPause(false);
    }

    /// <summary>Stops and closes the player.</summary>
    public void Dispose()
    {
        _player.Paused -= OnPaused;
        _player.EndReached -= OnEnded;
        _player.Stop();
        _player.Dispose();
        _media.Dispose();
    }

    // LibVLC's event thread: flags only, the UI thread acts on them.
    private void OnPaused(object? sender, EventArgs e) => _ready = true;

    private void OnEnded(object? sender, EventArgs e) => _ended = true;
}
