using LibVLCSharp.Shared;

namespace Lampion.App.Live;

/// <summary>
/// One pad's sound: a <see cref="PadShot"/> kept open and paused at the start so a press sounds at once, released on
/// the press and replaced by a fresh one. A new press cuts the previous play off. UI thread only.
/// </summary>
internal sealed class PadSound : IDisposable
{
    private readonly LibVLC _vlc;
    private PadShot? _armed;
    private PadShot? _playing;

    /// <summary>A sound for <paramref name="path"/> (full path), armed straight away.</summary>
    /// <param name="vlc">The LibVLC instance to play through.</param>
    /// <param name="source">The pad's path as stored in the project, to tell when it changes.</param>
    /// <param name="path">The file.</param>
    public PadSound(LibVLC vlc, string source, string path)
    {
        _vlc = vlc;
        Source = source;
        Path = path;
        Arm();
    }

    /// <summary>The pad's path as stored in the project.</summary>
    public string Source { get; }

    /// <summary>The file played.</summary>
    public string Path { get; }

    /// <summary>Plays the sound from its start at <paramref name="volume"/> (0..2).</summary>
    public void Fire(double volume)
    {
        _playing?.Dispose();
        // A press that beats the buffering (right after arming) opens a player straight away instead of waiting.
        if (_armed is { Ready: true } armed)
            _playing = armed;
        else
        {
            _armed?.Dispose();
            _playing = new PadShot(_vlc, Path, paused: false);
        }
        _armed = null;
        _playing.Play(volume);
        Arm();
    }

    /// <summary>Cuts the playing sound off; the next press still sounds at once.</summary>
    public void Stop()
    {
        _playing?.Dispose();
        _playing = null;
    }

    /// <summary>Closes a play that has finished.</summary>
    /// <remarks>Done here rather than from the end-of-file event: disposing a player on its own event thread can deadlock LibVLC.</remarks>
    public void Tidy()
    {
        if (_playing is { Ended: true })
            Stop();
    }

    /// <summary>Stops and closes both players.</summary>
    public void Dispose()
    {
        Stop();
        _armed?.Dispose();
        _armed = null;
    }

    private void Arm() => _armed ??= new PadShot(_vlc, Path, paused: true);
}
