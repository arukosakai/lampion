namespace Lampion.Core.Model.Timeline;

/// <summary>
/// Finds markers by time. Markers are kept in the order they were added, not by time, so every lookup scans
/// the whole list. Allocation-free, so scrubbing can call it on every pointer move.
/// </summary>
public static class MarkerSearch
{
    /// <summary>The marker closest to <paramref name="time"/>, if it is strictly within <paramref name="tolerance"/>.</summary>
    /// <param name="markers">The project's markers.</param>
    /// <param name="time">Seconds on the show timeline.</param>
    /// <param name="tolerance">Seconds; 0 or less never finds anything.</param>
    /// <returns>The marker, or null when none is close enough. The earlier one in the list wins a tie.</returns>
    public static Marker? Nearest(IReadOnlyList<Marker> markers, double time, double tolerance)
    {
        Marker? best = null;
        var bestDistance = tolerance;
        for (var i = 0; i < markers.Count; i++)
        {
            var distance = Math.Abs(markers[i].Time - time);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = markers[i];
            }
        }
        return best;
    }

    /// <summary>The first marker after <paramref name="time"/> (more than <paramref name="epsilon"/> later), or null.</summary>
    /// <param name="markers">The project's markers.</param>
    /// <param name="time">Seconds on the show timeline, usually the playhead.</param>
    /// <param name="epsilon">Seconds a marker must be past <paramref name="time"/>, so one under the playhead is skipped.</param>
    public static Marker? Next(IReadOnlyList<Marker> markers, double time, double epsilon = 1e-6)
    {
        Marker? best = null;
        for (var i = 0; i < markers.Count; i++)
        {
            var t = markers[i].Time;
            if (t > time + epsilon && (best is null || t < best.Time))
                best = markers[i];
        }
        return best;
    }

    /// <summary>The last marker before <paramref name="time"/> (more than <paramref name="epsilon"/> earlier), or null.</summary>
    /// <param name="markers">The project's markers.</param>
    /// <param name="time">Seconds on the show timeline, usually the playhead.</param>
    /// <param name="epsilon">Seconds a marker must be before <paramref name="time"/>, so one under the playhead is skipped.</param>
    public static Marker? Previous(IReadOnlyList<Marker> markers, double time, double epsilon = 1e-6)
    {
        Marker? best = null;
        for (var i = 0; i < markers.Count; i++)
        {
            var t = markers[i].Time;
            if (t < time - epsilon && (best is null || t > best.Time))
                best = markers[i];
        }
        return best;
    }
}
