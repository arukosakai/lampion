using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace Lampion.Core.Export;

/// <summary>Writes one lane as a hidden Scene plus the Sequence of linear fades bound to it.</summary>
internal sealed class QlcSequenceWriter(QlcFunctionWriter functions, string showName, int tolerance)
{
    /// <summary>Adds the lane's scene and sequence to <paramref name="output"/> and returns the show track playing it.</summary>
    /// <param name="lane">The lane.</param>
    /// <param name="vectors">The lane's sampled channel vectors.</param>
    /// <param name="times">Sample times in seconds.</param>
    /// <param name="trackIndex">Position of the returned track in the show.</param>
    /// <param name="showMs">Show length in milliseconds; the last step holds until then.</param>
    /// <param name="output">Receives the Scene and Sequence functions.</param>
    /// <param name="steps">Receives the number of steps written.</param>
    public XElement Write(QlcLane lane, IReadOnlyList<byte[]> vectors, IReadOnlyList<double> times, int trackIndex, int showMs, List<XElement> output, out int steps)
    {
        var ns = functions.Ns;
        var sceneId = functions.NextId();
        var scene = functions.Function(sceneId, "Scene", $"{showName} - {lane.Name} (scene)");
        scene.SetAttributeValue("Hidden", "True");
        foreach (var f in lane.Fixtures)
            scene.Add(new XElement(ns + "FixtureVal", new XAttribute("ID", f.QlcId), string.Join(",", Enumerable.Range(0, f.Profile.Channels.Count).Select(c => $"{c},0"))));
        output.Add(scene);

        var sequenceId = functions.NextId();
        var sequence = functions.Function(sequenceId, "Sequence", $"{showName} - {lane.Name}");
        sequence.SetAttributeValue("BoundScene", sceneId);
        sequence.Add(
            new XElement(ns + "Direction", "Forward"),
            new XElement(ns + "RunOrder", "SingleShot"),
            new XElement(ns + "SpeedModes", new XAttribute("FadeIn", "PerStep"), new XAttribute("FadeOut", "PerStep"), new XAttribute("Duration", "PerStep"))
        );
        var keep = FadeReducer.Simplify(vectors, times, tolerance);
        var previousMs = 0;
        for (var s = 0; s < keep.Count; s++)
        {
            var k = keep[s];
            var atMs = QlcFunctionWriter.Ms(times[k]);
            var text = StepText(lane, vectors[k], out var values);
            // The last step holds to the end of the show; every other step is a linear fade from the previous one.
            var hold = s == keep.Count - 1 ? Math.Max(0, showMs - atMs) : 0;
            sequence.Add(
                new XElement(
                    ns + "Step",
                    new XAttribute("Number", s),
                    new XAttribute("FadeIn", atMs - previousMs),
                    new XAttribute("Hold", hold),
                    new XAttribute("FadeOut", 0),
                    new XAttribute("Values", values),
                    text
                )
            );
            previousMs = atMs;
        }
        steps = keep.Count;
        output.Add(sequence);
        return functions.Track(trackIndex, lane.Name, sceneId, false, [new QlcShowFunction(sequenceId, 0, showMs, lane.Color)]);
    }

    /// <summary>A step's text, <c>fixtureId:channel,value,…:fixtureId:…</c> with 0-based channels.</summary>
    private static string StepText(QlcLane lane, byte[] vector, out int values)
    {
        var text = new StringBuilder();
        var offset = 0;
        foreach (var f in lane.Fixtures)
        {
            if (text.Length > 0)
                text.Append(':');
            text.Append(f.QlcId.ToString(CultureInfo.InvariantCulture)).Append(':');
            var count = f.Profile.Channels.Count;
            for (var c = 0; c < count; c++)
            {
                if (c > 0)
                    text.Append(',');
                text.Append(c.ToString(CultureInfo.InvariantCulture)).Append(',').Append(vector[offset + c].ToString(CultureInfo.InvariantCulture));
            }
            offset += count;
        }
        values = offset;
        return text.ToString();
    }
}
