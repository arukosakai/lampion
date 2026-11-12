using Lampion.Core.Color;
using Lampion.Core.Model.Live;

namespace Lampion.Core.Live;

/// <summary>
/// The console's moving parts: fader positions and pad states, by position in the <see cref="LivePatch"/>.
/// Any thread may write (UI, MIDI callbacks) and the engine reads every tick, without locks or allocation:
/// faders are last-writer-wins, and each pad is a press counter whose low bit is "on".
/// </summary>
public sealed class LiveControls
{
    /// <summary>Faders a console can have; positions past it are ignored.</summary>
    public const int MaxFaders = 128;

    /// <summary>Pads a console can have; positions past it are ignored.</summary>
    public const int MaxPads = 128;

    private readonly double[] _faders = new double[MaxFaders];
    private readonly int[] _pads = new int[MaxPads];

    /// <summary>Fader <paramref name="index"/>'s position, 0..1; 0 for positions out of range.</summary>
    public double Fader(int index) => (uint)index < MaxFaders ? Volatile.Read(ref _faders[index]) : 0;

    /// <summary>Moves fader <paramref name="index"/> to <paramref name="value"/> (clamped to 0..1).</summary>
    public void SetFader(int index, double value)
    {
        if ((uint)index < MaxFaders)
            Volatile.Write(ref _faders[index], Rgb.Clamp01(value));
    }

    /// <summary>True while pad <paramref name="index"/> is on.</summary>
    public bool IsPadOn(int index) => (PadGeneration(index) & 1) == 1;

    /// <summary>
    /// Counts every on/off change of pad <paramref name="index"/>; odd means on. The engine restarts a pad's
    /// effect whenever this moves to a new odd value, so a quick off-on still retriggers.
    /// </summary>
    public int PadGeneration(int index) => (uint)index < MaxPads ? Volatile.Read(ref _pads[index]) : 0;

    /// <summary>A press: toggles a <see cref="PadMode.Toggle"/> pad, switches a <see cref="PadMode.Flash"/> pad on.</summary>
    public void PressPad(int index, PadMode mode)
    {
        if ((uint)index >= MaxPads)
            return;
        if (mode == PadMode.Toggle)
            Interlocked.Increment(ref _pads[index]);
        else
            SetPad(index, on: true);
    }

    /// <summary>A release: switches a <see cref="PadMode.Flash"/> pad off; toggles ignore it.</summary>
    public void ReleasePad(int index, PadMode mode)
    {
        if (mode == PadMode.Flash)
            SetPad(index, on: false);
    }

    /// <summary>Switches pad <paramref name="index"/> on or off, whatever its mode.</summary>
    public void SetPad(int index, bool on)
    {
        if ((uint)index >= MaxPads)
            return;
        while (true)
        {
            var generation = Volatile.Read(ref _pads[index]);
            if (((generation & 1) == 1) == on || Interlocked.CompareExchange(ref _pads[index], generation + 1, generation) == generation)
                return;
        }
    }

    /// <summary>Switches every pad off.</summary>
    public void ReleaseAllPads()
    {
        for (var i = 0; i < MaxPads; i++)
            SetPad(i, on: false);
    }
}
