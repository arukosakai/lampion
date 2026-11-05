using System.Xml.Linq;
using Lampion.Core.Model;
using Lampion.Core.Model.Timeline;

namespace Lampion.Core.Export;

/// <summary>Builds every function of the exported show: a sequence per lane, media functions, and the Show that plays them.</summary>
internal sealed class QlcShowWriter(Project project, QlcExportOptions options, List<string> warnings)
{
    /// <summary>The functions to add to the workspace, in id order, and the number of sequence steps written.</summary>
    /// <param name="ns">The workspace namespace.</param>
    /// <param name="showName">Unique show name; also the folder the functions are filed under.</param>
    /// <param name="firstId">First free function id.</param>
    /// <param name="patch">The fixture patch.</param>
    /// <param name="steps">Receives the total step count.</param>
    public List<XElement> Build(XNamespace ns, string showName, int firstId, Dictionary<string, QlcPatchedFixture> patch, out int steps)
    {
        var functions = new QlcFunctionWriter(ns, firstId, showName.Replace('/', '-'));
        var output = new List<XElement>();
        var tracks = new List<XElement>();
        var showMs = QlcFunctionWriter.Ms(project.ContentEnd);
        steps = 0;

        var lanes = QlcLanePlanner.Plan(project, patch);
        var vectors = ShowSampler.Sample(project, lanes, options.SampleRate, out var times);
        var sequences = new QlcSequenceWriter(functions, showName, options.Tolerance);
        for (var l = 0; l < lanes.Count; l++)
        {
            tracks.Add(sequences.Write(lanes[l], vectors[l], times, tracks.Count, showMs, output, out var laneSteps));
            steps += laneSteps;
        }

        var media = new QlcMediaWriter(functions, showName, options, warnings);
        foreach (var track in project.Tracks.Where(t => t.Kind != TrackKind.Light))
            if (media.Write(track, tracks.Count, output) is { } showTrack)
                tracks.Add(showTrack);

        var show = new XElement(ns + "Function", new XAttribute("ID", functions.NextId()), new XAttribute("Type", "Show"), new XAttribute("Name", showName));
        show.Add(new XElement(ns + "TimeDivision", new XAttribute("Type", "Time"), new XAttribute("BPM", (int)Math.Round(project.Bpm))));
        show.Add(tracks);
        output.Add(show);
        return output;
    }
}
