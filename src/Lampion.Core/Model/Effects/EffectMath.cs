namespace Lampion.Core.Model.Effects;

/// <summary>Shared maths for the stock effects. Pure and allocation-free.</summary>
internal static class EffectMath
{
    /// <summary>The fractional part, always in 0..1 (also for negative values).</summary>
    public static double Frac(double v) => v - Math.Floor(v);

    /// <summary>Non-negative remainder of <paramref name="a"/> / <paramref name="n"/>, for cycling through palettes.</summary>
    public static int Mod(int a, int n) => ((a % n) + n) % n;

    /// <summary>
    /// The whole slot <paramref name="position"/> falls in, kept a few slots inside the int range (NaN gives 0). Callers
    /// loop from a slot or two before to this one, which never ends if it is <see cref="int.MaxValue"/>.
    /// </summary>
    public static int Slot(double position) => double.IsNaN(position) ? 0 : (int)Math.Clamp(Math.Floor(position), int.MinValue + 8.0, int.MaxValue - 8.0);

    /// <summary>One cycle of a waveform at phase 0..1, peaking at 1.</summary>
    public static double Shape(Waveform wave, double phase) =>
        wave switch
        {
            Waveform.Triangle => 1 - Math.Abs(2 * phase - 1),
            Waveform.Saw => 1 - phase,
            Waveform.Square => phase < 0.5 ? 1 : 0,
            _ => 0.5 - 0.5 * Math.Cos(2 * Math.PI * phase),
        };

    /// <summary>A repeatable pseudo-random number in 0..1, so random-looking effects scrub and export identically.</summary>
    public static double Hash(int slot, int index, int seed)
    {
        unchecked
        {
            var h = (uint)(slot * 73856093) ^ (uint)(index * 19349663) ^ (uint)(seed * 83492791);
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return (h & 0xFFFFFF) / (double)0x1000000;
        }
    }

    /// <summary>Smoothly interpolated value noise in 0..1, built on <see cref="Hash"/>.</summary>
    public static double Noise(double x, int index, int seed)
    {
        var i = (int)Math.Floor(x);
        var f = x - i;
        var s = f * f * (3 - 2 * f);
        var a = Hash(i, index, seed);
        var b = Hash(i + 1, index, seed);
        return a + (b - a) * s;
    }
}
