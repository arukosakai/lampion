namespace Lampion.App.Controls;

/// <summary>How a <see cref="ScrubValue"/> clamps, steps and displays its number.</summary>
/// <param name="Min">Smallest stored value.</param>
/// <param name="Max">Largest stored value.</param>
/// <param name="Step">Change per pixel dragged, in stored units.</param>
/// <param name="Scale">Display units per stored unit, e.g. 100 for a 0..1 value shown as %.</param>
/// <param name="Format">.NET number format for the displayed (scaled) value, invariant culture.</param>
/// <param name="Unit">Suffix shown after the number, including any leading space (" s", "%").</param>
internal sealed record ScrubRange(double Min, double Max, double Step, double Scale = 1, string Format = "0.##", string Unit = "")
{
    /// <summary>A 0..1 value shown as a whole percentage.</summary>
    public static ScrubRange Percent { get; } = new(0, 1, 0.005, 100, "0", "%");
}
