using Lampion.App.Controls;

namespace Lampion.App.Views.Inspector;

/// <summary>A timing property that can be shown in its own unit or in BPM.</summary>
/// <param name="Native">How the stored value is shown normally (Hz, /s or s).</param>
/// <param name="Bpm">How the same stored value is shown in beats per minute.</param>
/// <param name="IsPeriod">True when the stored value is seconds per beat (a step time), false for cycles per second.</param>
internal sealed record TempoScale(ScrubRange Native, ScrubRange Bpm, bool IsPeriod)
{
    /// <summary>Cycles per second.</summary>
    public static TempoScale Rate { get; } =
        new(new(0.05, 30, 0.02, Unit: " Hz"), new(0.05, 30, 1 / 60.0, 60, "0.#", " BPM"), IsPeriod: false);

    /// <summary>Signed cycles per second; negative runs backwards.</summary>
    public static TempoScale Speed { get; } =
        new(new(-4, 4, 0.005, Unit: " /s"), new(-4, 4, 1 / 60.0, 60, "0.#", " BPM"), IsPeriod: false);

    /// <summary>Seconds per step.</summary>
    public static TempoScale StepTime { get; } =
        new(new(0.01, 8, 0.005, Unit: " s"), new(0.01, 8, 1, 60, "0.#", " BPM", Reciprocal: true), IsPeriod: true);

    /// <summary>Seconds between repeats; 0 means no repeat, shown as 0 BPM.</summary>
    public static TempoScale Interval { get; } =
        new(new(0, 8, 0.005, Unit: " s"), new(0, 8, 1, 60, "0.#", " BPM", Reciprocal: true), IsPeriod: true);

    /// <summary>The native unit's name for the unit toggle, e.g. "Hz".</summary>
    public string NativeUnit => Native.Unit.Trim();

    /// <summary>The stored value that beats <paramref name="bpm"/> times a minute, clamped to the range.</summary>
    /// <param name="bpm">Beats per minute, above 0.</param>
    /// <param name="current">The current stored value; its sign is kept, so a reversed speed stays reversed.</param>
    public double FromBpm(double bpm, double current)
    {
        var value = IsPeriod ? 60 / bpm : bpm / 60;
        if (current < 0)
            value = -value;
        return Math.Clamp(value, Native.Min, Native.Max);
    }
}
