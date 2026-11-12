namespace Lampion.App.Controls;

/// <summary>How a <see cref="ScrubValue"/> clamps, steps and displays its number.</summary>
/// <param name="Min">Smallest stored value.</param>
/// <param name="Max">Largest stored value.</param>
/// <param name="Step">Change per pixel dragged: in stored units, or in displayed units when <paramref name="Reciprocal"/>.</param>
/// <param name="Scale">Display units per stored unit, e.g. 100 for a 0..1 value shown as %; the numerator when <paramref name="Reciprocal"/>.</param>
/// <param name="Format">.NET number format for the displayed (scaled) value, invariant culture.</param>
/// <param name="Unit">Suffix shown after the number, including any leading space (" s", "%").</param>
/// <param name="Reciprocal">Shows <paramref name="Scale"/> / value, e.g. 60 / seconds for a period shown as BPM. A stored value ≤ 0 shows as 0, and 0 typed or dragged to stores 0.</param>
internal sealed record ScrubRange(double Min, double Max, double Step, double Scale = 1, string Format = "0.##", string Unit = "", bool Reciprocal = false)
{
    /// <summary>A 0..1 value shown as a whole percentage.</summary>
    public static ScrubRange Percent { get; } = new(0, 1, 0.005, 100, "0", "%");

    /// <summary>Change of the displayed value per pixel dragged.</summary>
    public double DisplayStep => Reciprocal ? Step : Step * Scale;

    /// <summary>The number shown for a stored value.</summary>
    public double ToDisplay(double stored) =>
        !Reciprocal ? stored * Scale
        : stored > 0 ? Scale / stored
        : 0;

    /// <summary>The stored value for a shown number, not clamped.</summary>
    public double FromDisplay(double shown) =>
        !Reciprocal ? shown / Scale
        : shown > 0 ? Scale / shown
        : 0;
}
