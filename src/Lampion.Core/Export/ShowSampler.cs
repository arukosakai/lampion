using Lampion.Core.Engine;
using Lampion.Core.Model;

namespace Lampion.Core.Export;

/// <summary>Samples the rendered show at a fixed rate into one vector of channel values per lane per sample.</summary>
internal static class ShowSampler
{
    /// <summary>
    /// Renders the show from 0 to its content end (inclusive) at <paramref name="sampleRate"/> (clamped to
    /// 1..100 per second). Rendering reuses one allocation-free renderer; only the stored vectors allocate.
    /// </summary>
    /// <param name="project">The show.</param>
    /// <param name="lanes">The lanes to collect.</param>
    /// <param name="sampleRate">Samples per second.</param>
    /// <param name="times">Receives the sample times in seconds, ascending.</param>
    /// <returns>Per lane, per sample: the lane's fixtures' channels back to back (<see cref="QlcLane.Width"/> bytes).</returns>
    public static List<byte[]>[] Sample(Project project, IReadOnlyList<QlcLane> lanes, double sampleRate, out List<double> times)
    {
        var renderer = new ShowRenderer(project);
        var end = project.ContentEnd;
        var rate = Math.Clamp(sampleRate, 1, 100);
        var count = (int)Math.Ceiling(end * rate);
        times = new List<double>(count + 1);
        var vectors = new List<byte[]>[lanes.Count];
        var widths = new int[lanes.Count];
        for (var l = 0; l < lanes.Count; l++)
        {
            vectors[l] = new List<byte[]>(count + 1);
            widths[l] = lanes[l].Width;
        }

        for (var k = 0; k <= count; k++)
        {
            var t = Math.Min(end, k / rate);
            times.Add(t);
            if (lanes.Count == 0)
                continue;
            var frame = renderer.RenderFixtures(t);
            for (var l = 0; l < lanes.Count; l++)
            {
                var vector = new byte[widths[l]];
                var offset = 0;
                foreach (var fixture in lanes[l].Fixtures)
                {
                    var values = frame[fixture.FixtureIndex];
                    values.CopyTo(vector.AsSpan(offset));
                    offset += values.Length;
                }
                vectors[l].Add(vector);
            }
        }
        return vectors;
    }
}
