using System.Diagnostics;
using Lampion.Core.Color;
using Lampion.Core.Engine;
using Lampion.Core.Model.Effects;
using Lampion.Core.Model.Live;

namespace Lampion.Core.Live;

/// <summary>
/// Renders live DMX: the playing show on the fixtures it owns, then the console (faders, then pads) on every
/// other fixture. <see cref="Render"/> runs on one thread (the <see cref="LiveLoop"/>) and allocates nothing;
/// <see cref="Scene"/> can be swapped from any thread and is picked up on the next render.
/// </summary>
public sealed class LiveEngine
{
    /// <summary>Seconds that effects timed over a clip (colour fade, hit) take on a pad, since a pad has no length.</summary>
    public const double PadEffectSpan = 4;

    private readonly int[] _padSeen = new int[LiveControls.MaxPads];
    private readonly double[] _padTime = new double[LiveControls.MaxPads];
    private readonly double[] _padIntensity = new double[LiveControls.MaxPads];
    private readonly double[] _padSpeed = new double[LiveControls.MaxPads];
    private readonly double[] _padHue = new double[LiveControls.MaxPads];
    private LiveScene? _scene;
    private LiveScene? _current;
    private double _time;
    private long _lastTimestamp;
    private bool _ended;
    private long _publishedTime;
    private volatile bool _publishedEnded;
    private volatile string? _lastFault;
    private int _faults;

    /// <summary>An idle engine reading <paramref name="controls"/>.</summary>
    public LiveEngine(LiveControls controls) => Controls = controls;

    /// <summary>The faders and pads it reads every render.</summary>
    public LiveControls Controls { get; }

    /// <summary>What to render; null renders nothing. Any thread; takes effect on the next <see cref="Render"/>.</summary>
    public LiveScene? Scene
    {
        get => Volatile.Read(ref _scene);
        set => Volatile.Write(ref _scene, value);
    }

    /// <summary>Seconds into the playing show, as of the last render. Any thread.</summary>
    public double ShowTime => BitConverter.Int64BitsToDouble(Volatile.Read(ref _publishedTime));

    /// <summary>True once a non-looping show has played to its end; its fixtures are back on the console. Any thread.</summary>
    public bool ShowEnded => _publishedEnded;

    /// <summary>
    /// Faults contained so far: a show that failed to evaluate (its fixtures held their last look) or a pad that threw
    /// (it was switched off). Any thread.
    /// </summary>
    public int FaultCount => Volatile.Read(ref _faults);

    /// <summary>The latest contained fault in words, or null. Any thread.</summary>
    public string? LastFault => _lastFault;

    /// <summary>
    /// Renders the frame due at <paramref name="timestamp"/> (a <see cref="Stopwatch"/> timestamp) into
    /// <paramref name="frame"/>, replacing its contents. The show's time comes from its <see cref="ShowClock"/>; pads advance by
    /// the time since the last render. Engine thread only; allocation-free once the frame's universes exist.
    /// </summary>
    public void Render(long timestamp, DmxFrame frame)
    {
        var seconds = _lastTimestamp == 0 ? 0 : Math.Max(0, (timestamp - _lastTimestamp) / (double)Stopwatch.Frequency);
        _lastTimestamp = timestamp;
        var scene = Volatile.Read(ref _scene);
        if (!ReferenceEquals(scene, _current))
            Switch(scene);
        frame.Clear();
        if (scene is null)
            return;

        var patch = scene.Patch;
        var states = patch.States.AsSpan();
        states.Clear();
        var cue = PlayShow(scene.Cue, states, timestamp);
        var owned = cue is null ? ReadOnlySpan<bool>.Empty : cue.Owned;
        ApplyFaders(patch, owned, states);
        ApplyPads(patch, owned, states, seconds);
        Map(patch, frame);

        Volatile.Write(ref _publishedTime, BitConverter.DoubleToInt64Bits(_time));
        _publishedEnded = _ended;
    }

    private void Switch(LiveScene? scene)
    {
        // Same clock means the same show, re-mapped after an edit: keep playing from where it is.
        if (!ReferenceEquals(scene?.Cue?.Clock, _current?.Cue?.Clock))
        {
            _time = 0;
            _ended = false;
        }
        _current = scene;
    }

    private ShowCue? PlayShow(ShowCue? cue, Span<FixtureState> states, long timestamp)
    {
        if (cue is null || _ended)
            return null;
        _time = cue.Clock.TimeAt(timestamp);
        if (_time >= cue.Duration)
        {
            if (!cue.Loop || cue.Duration <= 0)
            {
                _ended = true;
                return null;
            }
            // With music, a loop restarts when the music does; until then the show holds its last moment.
            _time = cue.Clock.FollowsMedia ? Math.BitDecrement(cue.Duration) : _time % cue.Duration;
        }
        var looks = cue.Looks;
        try
        {
            cue.Evaluator.Evaluate(double.IsFinite(_time) ? _time : 0, looks.Back);
            looks.Swap();
        }
        catch (Exception ex)
        {
            Fault("The show", ex);
        }
        var show = looks.Front;
        for (var i = 0; i < show.Length; i++)
            if (cue.RigIndexOf(i) is var rig and >= 0)
                states[rig] = show[i];
        return cue;
    }

    private void ApplyFaders(LivePatch patch, ReadOnlySpan<bool> owned, Span<FixtureState> states)
    {
        var level = patch.Level;
        var hue = patch.Hue;
        var saturation = patch.Saturation;
        var touched = patch.Touched;
        var manual = patch.Manual;
        level.AsSpan().Fill(-1);
        hue.AsSpan().Fill(-1);
        saturation.AsSpan().Fill(-1);
        touched.AsSpan().Clear();
        manual.AsSpan().Clear();
        _padIntensity.AsSpan().Fill(1);
        _padSpeed.AsSpan().Fill(1);
        _padHue.AsSpan().Clear();

        var faders = patch.Faders;
        for (var f = 0; f < faders.Length; f++)
        {
            var fader = faders[f];
            var value = Controls.Fader(f);
            switch (fader.Target)
            {
                case LiveFaderTarget.PadIntensity when fader.Pad >= 0:
                    _padIntensity[fader.Pad] = value;
                    continue;
                case LiveFaderTarget.PadSpeed when fader.Pad >= 0:
                    _padSpeed[fader.Pad] = Math.Pow(4, value * 2 - 1);
                    continue;
                case LiveFaderTarget.PadHueShift when fader.Pad >= 0:
                    _padHue[fader.Pad] = value;
                    continue;
            }
            var targets = fader.Fixtures;
            for (var t = 0; t < targets.Length; t++)
            {
                var j = targets[t];
                touched[j] = true;
                switch (fader.Target)
                {
                    case LiveFaderTarget.Intensity:
                        level[j] = Math.Max(level[j], value);
                        break;
                    case LiveFaderTarget.Hue:
                        hue[j] = value;
                        break;
                    case LiveFaderTarget.Saturation:
                        saturation[j] = value;
                        break;
                    case LiveFaderTarget.Role:
                        manual[j].SetRole(fader.Role, manual[j].TryGetRole(fader.Role, out var below) ? Math.Max(below, value) : value);
                        break;
                }
            }
        }

        for (var j = 0; j < states.Length; j++)
        {
            if (!touched[j] || IsOwned(owned, j))
                continue;
            var color = hue[j] >= 0 ? Rgb.FromHsv(hue[j], saturation[j] >= 0 ? saturation[j] : 1, 1) : Rgb.White;
            manual[j].Light = color * Math.Max(0, level[j]);
            states[j] = manual[j];
        }
    }

    private void ApplyPads(LivePatch patch, ReadOnlySpan<bool> owned, Span<FixtureState> states, double seconds)
    {
        var pads = patch.Pads;
        FixtureState layer = default;
        for (var p = 0; p < pads.Length; p++)
        {
            var generation = Controls.PadGeneration(p);
            var restarted = generation != _padSeen[p];
            _padSeen[p] = generation;
            if ((generation & 1) == 0)
                continue;
            _padTime[p] = restarted ? 0 : _padTime[p] + seconds * _padSpeed[p];

            var pad = pads[p];
            var targets = pad.Fixtures;
            var time = _padTime[p];
            var shift = _padHue[p];
            try
            {
                for (var k = 0; k < targets.Length; k++)
                {
                    var j = targets[k];
                    if (IsOwned(owned, j))
                        continue;
                    var sample = pad.Effect.Sample(new EffectContext(time, PadEffectSpan, k, targets.Length));
                    var color = sample.Color;
                    if (shift != 0)
                    {
                        var (h, s, v) = color.ToHsv();
                        color = Rgb.FromHsv(h + shift, s, v);
                    }
                    layer = default;
                    layer.Light = color * sample.Intensity;
                    states[j].Blend(in layer, _padIntensity[p], pad.Blend);
                }
            }
            catch (Exception ex)
            {
                // One broken pad must not cost the frame: switch it off and let everything else carry on.
                Controls.SetPad(p, on: false);
                Fault("A pad", ex);
            }
        }
    }

    private void Fault(string what, Exception ex)
    {
        _lastFault = what + " failed and was contained: " + ex.Message;
        Interlocked.Increment(ref _faults);
    }

    private static bool IsOwned(ReadOnlySpan<bool> owned, int fixture) => (uint)fixture < (uint)owned.Length && owned[fixture];

    // Same placement rules as ShowRenderer: later fixtures win where footprints overlap, past 512 is dropped.
    private static void Map(LivePatch patch, DmxFrame frame)
    {
        var index = patch.Index;
        var fixtures = patch.Rig.Fixtures;
        var states = patch.States;
        for (var i = 0; i < states.Length; i++)
        {
            if (index.ProfileOf(i) is not { } profile)
                continue;
            var fixture = fixtures[i];
            var values = patch.Channels.AsSpan(0, profile.Channels.Count);
            DmxMapper.Map(fixture, profile, in index.LayoutOf(i), in states[i], values);
            var universe = frame.Universe(fixture.Universe);
            for (var c = 0; c < values.Length; c++)
            {
                var address = fixture.Address - 1 + c;
                if (address is >= 0 and < DmxFrame.UniverseSize)
                    universe[address] = values[c];
            }
        }
    }
}
