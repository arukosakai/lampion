using System.Globalization;

namespace Lampion.App.Controls;

/// <summary>
/// Labels such as "Pan 45°", formatted once per whole degree and reused, so readouts that change while
/// dragging don't format a string every pointer move. Not thread-safe; use from the UI thread.
/// </summary>
/// <param name="prefix">Text before the number, e.g. "Pan ".</param>
internal sealed class DegreeLabels(string prefix)
{
    // "0" formatting keeps the sign of values that round to zero ("-0"), so that case gets its own slot.
    private const int NegativeZero = int.MinValue;

    private readonly Dictionary<int, string> _labels = [];

    /// <summary>The label for <paramref name="degrees"/>, rounded to a whole degree the way the "0" format rounds.</summary>
    public string For(double degrees)
    {
        var rounded = Math.Round(degrees, MidpointRounding.AwayFromZero);
        var key = rounded == 0 && degrees < 0 ? NegativeZero : (int)rounded;
        if (!_labels.TryGetValue(key, out var label))
        {
            label = prefix + degrees.ToString("0", CultureInfo.CurrentCulture) + "°";
            _labels[key] = label;
        }
        return label;
    }
}
