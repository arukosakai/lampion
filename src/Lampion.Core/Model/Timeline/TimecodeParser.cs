using System.Globalization;

namespace Lampion.Core.Model.Timeline;

/// <summary>Reads a show time the user typed, for "go to time".</summary>
public static class TimecodeParser
{
    private static readonly double[] ClockUnits = [1, 60, 3600];

    /// <summary>
    /// Parses <paramref name="text"/> into seconds. Accepts <c>HH:MM:SS:FF</c> timecode, <c>H:M:S</c>,
    /// <c>M:S</c> or plain seconds (decimals with <c>.</c> or <c>,</c>, an optional trailing <c>s</c>).
    /// A leading <c>+</c> or <c>-</c> moves relative to <paramref name="current"/>.
    /// </summary>
    /// <param name="text">What was typed; surrounding whitespace is ignored.</param>
    /// <param name="current">The playhead, in seconds, that relative times count from.</param>
    /// <param name="fps">Frames per second of the <c>FF</c> field; 1 or more.</param>
    /// <param name="seconds">The time in seconds, never below 0; 0 when parsing fails.</param>
    /// <returns>False when the text isn't a time.</returns>
    public static bool TryParse(string? text, double current, int fps, out double seconds)
    {
        seconds = 0;
        var s = text?.Trim().Replace(',', '.');
        if (string.IsNullOrEmpty(s))
            return false;

        var sign = s[0] switch { '+' => 1, '-' => -1, _ => 0 };
        if (sign != 0)
            s = s[1..].TrimStart();
        if (s.EndsWith('s'))
            s = s[..^1].TrimEnd();

        if (!TryParseClock(s, fps, out var value))
            return false;
        seconds = Math.Max(0, sign == 0 ? value : current + sign * value);
        return true;
    }

    private static bool TryParseClock(string s, int fps, out double value)
    {
        value = 0;
        var parts = s.Split(':');
        if (parts.Length > 4)
            return false;

        // Four fields are timecode: the last one counts frames, not seconds.
        var clockFields = parts.Length == 4 ? 3 : parts.Length;
        for (var i = 0; i < parts.Length; i++)
        {
            var isLast = i == parts.Length - 1;
            if (!double.TryParse(parts[i], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var field))
                return false;
            if (!isLast && field != Math.Floor(field))
                return false;
            if (i < clockFields)
                value += field * ClockUnits[clockFields - 1 - i];
            else
                value += field / Math.Max(1, fps);
        }
        return true;
    }
}
