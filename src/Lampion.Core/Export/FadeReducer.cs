namespace Lampion.Core.Export;

/// <summary>Reduces a sampled channel vector over time to the fewest linear fades that stay within a tolerance.</summary>
internal static class FadeReducer
{
    /// <summary>
    /// Indices of the samples to keep so that a linear fade between consecutive kept samples stays within
    /// <paramref name="tolerance"/> of every sample it skips. Greedy, one pass: for each channel it narrows
    /// the range of slopes from the last kept sample that every skipped sample allows. Always keeps the
    /// first and last sample; empty input gives an empty list.
    /// </summary>
    /// <param name="vectors">One channel vector per sample, all the same length.</param>
    /// <param name="times">Sample times in seconds, ascending, parallel to <paramref name="vectors"/>.</param>
    /// <param name="tolerance">Largest allowed error in DMX steps.</param>
    public static List<int> Simplify(IReadOnlyList<byte[]> vectors, IReadOnlyList<double> times, int tolerance)
    {
        var keep = new List<int>();
        if (vectors.Count == 0)
            return keep;
        keep.Add(0);
        var width = vectors[0].Length;
        var anchor = 0;
        var lo = new double[width];
        var hi = new double[width];
        Array.Fill(lo, double.NegativeInfinity);
        Array.Fill(hi, double.PositiveInfinity);

        for (var j = 1; j < vectors.Count; j++)
        {
            var dt = times[j] - times[anchor];
            var fits = dt > 0;
            for (var c = 0; fits && c < width; c++)
            {
                var slope = (vectors[j][c] - vectors[anchor][c]) / dt;
                fits = slope >= lo[c] - 1e-9 && slope <= hi[c] + 1e-9;
            }
            if (!fits)
            {
                // The previous sample is the furthest a single fade from the anchor can reach.
                anchor = j - 1;
                keep.Add(anchor);
                Array.Fill(lo, double.NegativeInfinity);
                Array.Fill(hi, double.PositiveInfinity);
                dt = times[j] - times[anchor];
            }
            if (dt <= 0)
                continue;
            for (var c = 0; c < width; c++)
            {
                var delta = vectors[j][c] - vectors[anchor][c];
                lo[c] = Math.Max(lo[c], (delta - tolerance) / dt);
                hi[c] = Math.Min(hi[c], (delta + tolerance) / dt);
            }
        }
        if (keep[^1] != vectors.Count - 1)
            keep.Add(vectors.Count - 1);
        return keep;
    }
}
