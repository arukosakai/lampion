using System.Xml.Linq;
using Lampion.Core.Model.Timeline;

namespace Lampion.Core.Export;

/// <summary>Writes an audio or video track's clips as QLC+ Audio/Video functions on one show track.</summary>
internal sealed class QlcMediaWriter(QlcFunctionWriter functions, string showName, QlcExportOptions options, List<string> warnings)
{
    /// <summary>Adds a function per media clip to <paramref name="output"/>; returns the show track, or null when the track has no media clips.</summary>
    public XElement? Write(Track track, int trackIndex, List<XElement> output)
    {
        var shows = new List<QlcShowFunction>();
        foreach (var clip in track.Clips.OfType<MediaClip>().OrderBy(c => c.Start))
        {
            var isVideo = track.Kind == TrackKind.Video;
            var id = functions.NextId();
            var function = functions.Function(id, isVideo ? "Video" : "Audio", $"{showName} - {clip.Name}");
            function.Add(new XElement(functions.Ns + "RunOrder", "SingleShot"));
            var source = new XElement(functions.Ns + "Source", MediaPath(clip.Path));
            if (isVideo)
                source.SetAttributeValue("Fullscreen", 1);
            function.Add(source);
            output.Add(function);
            if (clip.MediaOffset > 0.01)
                warnings.Add($"\"{clip.Name}\" is trimmed {clip.MediaOffset:0.##}s into the file; QLC+ 4 always plays media from the start, so trim the file itself.");
            shows.Add(new QlcShowFunction(id, QlcFunctionWriter.Ms(clip.Start), QlcFunctionWriter.Ms(clip.Duration), isVideo ? "#938C14" : "#608053"));
        }
        return shows.Count > 0 ? functions.Track(trackIndex, track.Name, null, track.Muted, shows) : null;
    }

    /// <summary>The path QLC+ should open: relative to the output folder when the file is reachable from it, else absolute, else as stored.</summary>
    private string MediaPath(string path)
    {
        var absolute = Path.IsPathRooted(path) || options.ProjectDirectory is null ? path : Path.GetFullPath(Path.Combine(options.ProjectDirectory, path));
        if (options.OutputDirectory is null || !Path.IsPathRooted(absolute))
            return path;
        var relative = Path.GetRelativePath(options.OutputDirectory, absolute);
        return Path.IsPathRooted(relative) ? absolute : relative.Replace('\\', '/');
    }
}
