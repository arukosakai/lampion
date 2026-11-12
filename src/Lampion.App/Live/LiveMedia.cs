using System.Diagnostics;
using Lampion.Core.Live;
using Lampion.Core.Model.Timeline;
using LibVLCSharp.Shared;

namespace Lampion.App.Live;

/// <summary>
/// Plays a live show's music and makes it the show's master clock: the song's position reports steer the cue's
/// <see cref="ShowClock"/>, so the lights follow the audio the audience hears rather than the other way round.
/// A show that opens with music holds its first frame while the music buffers, then both start together; clips that come in later
/// are opened paused a second early and released on time. UI thread only, except the position callbacks.
/// </summary>
internal sealed partial class LiveMedia : IDisposable
{
    // Clips that start later are opened this many seconds early, paused, so releasing them is quick.
    private const double Preroll = 1.0;

    // A clip this close to the show's start counts as opening the show, so the show waits for it.
    private const double OpeningClip = 0.05;

    // Most the show waits for its opening music to buffer before starting without it (a slow disk, a damaged file).
    private static readonly long BufferTimeout = Stopwatch.Frequency * 3;

    private readonly LibVLC? _vlc;
    private readonly List<Voice> _voices = [];
    private ShowCue? _cue;
    private Voice? _master;
    private double _lightDelay;
    private long _startRequested;

    /// <summary>Starts LibVLC for live audio; on failure shows run on their own clock and <see cref="Error"/> says why.</summary>
    public LiveMedia()
    {
        try
        {
            _vlc = new LibVLC("--quiet", "--no-video");
        }
        catch (Exception ex) when (ex is VLCException or DllNotFoundException or InvalidOperationException)
        {
            Error = $"Live audio unavailable (LibVLC): {ex.Message}";
        }
    }

    /// <summary>Why live audio can't play, or null.</summary>
    public string? Error { get; }

    /// <summary>Seconds the lights run behind the music (negative: ahead). Any thread.</summary>
    public double LightDelay
    {
        get => Volatile.Read(ref _lightDelay);
        set => Volatile.Write(ref _lightDelay, value);
    }

    /// <summary>True while a show's music is playing and steering its clock.</summary>
    public bool HasMusic => _voices.Count > 0;

    /// <summary>
    /// Plays <paramref name="cue"/>'s music and hands it the cue's clock. Returns false when the show has no playable
    /// music (none, files missing, or no LibVLC): the caller then starts the clock itself.
    /// </summary>
    /// <param name="cue">The show about to go live.</param>
    /// <param name="missing">Music files that couldn't be found, for the status line.</param>
    public bool Play(ShowCue cue, out IReadOnlyList<string> missing)
    {
        Stop();
        var gone = new List<string>();
        missing = gone;
        if (_vlc is null)
            return false;
        foreach (var clip in cue.AudioClips)
            if (File.Exists(clip.Path))
                _voices.Add(new Voice(_vlc, clip, this));
            else
                gone.Add(Path.GetFileName(clip.Path));
        if (_voices.Count == 0)
            return false;
        _cue = cue;
        cue.Clock.FollowsMedia = true;
        Begin();
        return true;
    }

    /// <summary>Starts clips as the show reaches them, picks the clip that steers the clock, and restarts a looping show's music. Call every UI tick.</summary>
    public void Tick()
    {
        if (_cue is not { } cue)
            return;
        var clock = cue.Clock;
        if (_startRequested != 0)
        {
            StartWhenBuffered(cue);
            return;
        }
        var t = clock.TimeAt(Stopwatch.GetTimestamp());
        if (cue.Loop && clock.IsRunning && t >= cue.Duration)
        {
            Begin();
            return;
        }
        Voice? master = null;
        foreach (var voice in _voices)
        {
            var clip = voice.Clip;
            if (t >= clip.End)
                voice.Stop();
            else if (t >= clip.Start - Preroll)
            {
                voice.Prepare(Math.Max(0, t - clip.Start) + clip.MediaOffset, paused: t < clip.Start);
                if (t >= clip.Start)
                    voice.Release();
            }
            if (master is null && voice.IsPlaying)
                master = voice;
        }
        Volatile.Write(ref _master, master);
    }

    /// <summary>Stops the music; the cue's clock keeps whatever time it had.</summary>
    public void Stop()
    {
        Volatile.Write(ref _master, null);
        _startRequested = 0;
        foreach (var voice in _voices)
            voice.Dispose();
        _voices.Clear();
        _cue = null;
    }

    /// <summary>Stops the music and LibVLC.</summary>
    public void Dispose()
    {
        Stop();
        _vlc?.Dispose();
    }

    // (Re)starts the show from 0: music that opens the show starts now and the clock waits for it; otherwise the
    // clock runs from now and later clips are prerolled by Tick.
    private void Begin()
    {
        var cue = _cue!;
        foreach (var voice in _voices)
            voice.Stop();
        var opening = _voices.Find(v => v.Clip.Start <= OpeningClip);
        if (opening is null)
        {
            cue.Clock.Start(Stopwatch.GetTimestamp(), 0);
            return;
        }
        // Opened paused so VLC buffers first: its "playing" comes ~300 ms before any sound, but a buffered release is near instant.
        cue.Clock.Hold(0);
        foreach (var voice in _voices)
            if (voice.Clip.Start <= OpeningClip)
                voice.Prepare(voice.Clip.MediaOffset, paused: true);
        _startRequested = Stopwatch.GetTimestamp();
        Volatile.Write(ref _master, opening);
    }

    // Releases the opening music and starts the clock in the same instant; the position reports then trim what's left.
    private void StartWhenBuffered(ShowCue cue)
    {
        var now = Stopwatch.GetTimestamp();
        var buffered = true;
        foreach (var voice in _voices)
            if (voice.Clip.Start <= OpeningClip)
                buffered &= voice.Ready;
        if (!buffered && now - _startRequested < BufferTimeout)
            return;
        _startRequested = 0;
        foreach (var voice in _voices)
            if (voice.Clip.Start <= OpeningClip)
                voice.Release();
        cue.Clock.Start(now, -LightDelay);
    }

    // Runs on LibVLC's event thread: keep it to arithmetic and the clock.
    private void OnPosition(Voice voice, double mediaSeconds, long timestamp)
    {
        if (!ReferenceEquals(Volatile.Read(ref _master), voice) || _cue is not { } cue)
            return;
        var clip = voice.Clip;
        cue.Clock.Sync(timestamp, clip.Start + mediaSeconds - clip.MediaOffset - LightDelay);
    }

    /// <summary>The audio clip the clock is following, for the status line; null when none.</summary>
    public MediaClip? Following => Volatile.Read(ref _master)?.Clip;
}
