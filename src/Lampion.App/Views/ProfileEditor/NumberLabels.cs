using System.Globalization;

namespace Lampion.App.Views.ProfileEditor;

/// <summary>
/// Pre-formatted readouts for whole numbers 0..max with a unit ("42%", "180°"), so sliders can show their
/// value while dragging without formatting a string per change.
/// </summary>
internal sealed class NumberLabels
{
    private readonly string[] _labels;

    /// <param name="max">Largest value with a label; larger values read as it.</param>
    /// <param name="unit">Appended to every number; empty for none.</param>
    public NumberLabels(int max, string unit)
    {
        _labels = new string[max + 1];
        for (var i = 0; i <= max; i++)
            _labels[i] = i.ToString(CultureInfo.CurrentCulture) + unit;
    }

    /// <summary>DMX values, 0–255, no unit.</summary>
    public static NumberLabels Dmx { get; } = new(255, "");

    /// <summary>Percentages, 0–100 %.</summary>
    public static NumberLabels Percent { get; } = new(100, "%");

    /// <summary>Angles, 0–360°.</summary>
    public static NumberLabels Degrees { get; } = new(360, "°");

    /// <summary>The label for <paramref name="value"/>, clamped to 0..max.</summary>
    public string Of(int value) => _labels[Math.Clamp(value, 0, _labels.Length - 1)];

    /// <summary>The label for <paramref name="value"/> rounded as the <c>"0"</c> format does (halves away from zero).</summary>
    public string Of(double value) => Of((int)Math.Round(value, MidpointRounding.AwayFromZero));
}
