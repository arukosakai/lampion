namespace Lampion.App.Controls.Timeline;

/// <summary>
/// Ruler time labels ("1:05", "0:02.50"), formatted once per tick. Ticks are identified by their index and
/// spacing, so scrolling back and forth reuses the same strings. UI thread only.
/// </summary>
internal sealed class RulerLabelCache
{
    private const int Limit = 4096;

    private readonly Dictionary<(long Index, double Step), string> _labels = [];

    /// <summary>The label of tick <paramref name="index"/> (at <paramref name="index"/> × <paramref name="step"/> seconds).</summary>
    /// <param name="index">Tick number from time 0.</param>
    /// <param name="step">Seconds between labelled ticks; below 1 the label shows hundredths.</param>
    public string Get(long index, double step)
    {
        if (_labels.TryGetValue((index, step), out var label))
            return label;
        // Zooming visits many spacings; a crude bound keeps it from growing forever.
        if (_labels.Count >= Limit)
            _labels.Clear();
        label = Format(index * step, step);
        _labels[(index, step)] = label;
        return label;
    }

    private static string Format(double t, double step)
    {
        var minutes = (int)(t / 60);
        var seconds = t - minutes * 60;
        return step < 1 ? $"{minutes}:{seconds:00.00}" : $"{minutes}:{seconds:00}";
    }
}
