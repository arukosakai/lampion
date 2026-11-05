using System.Xml.Linq;

namespace Lampion.Core.Export;

/// <summary>Hands out QLC+ function ids in order and writes the elements every function and show track share.</summary>
internal sealed class QlcFunctionWriter(XNamespace ns, int firstId, string path)
{
    private int _nextId = firstId;

    /// <summary>The workspace namespace.</summary>
    public XNamespace Ns { get; } = ns;

    /// <summary>The next unused function id.</summary>
    public int NextId() => _nextId++;

    /// <summary>A Function element in the show's folder, with zero speeds.</summary>
    public XElement Function(int id, string type, string name)
    {
        var element = new XElement(Ns + "Function", new XAttribute("ID", id), new XAttribute("Type", type), new XAttribute("Name", name), new XAttribute("Path", path));
        element.Add(new XElement(Ns + "Speed", new XAttribute("FadeIn", 0), new XAttribute("FadeOut", 0), new XAttribute("Duration", 0)));
        return element;
    }

    /// <summary>A Show Manager track playing <paramref name="functions"/>.</summary>
    /// <param name="index">Track position, 0-based.</param>
    /// <param name="name">Track name.</param>
    /// <param name="sceneId">The scene a light lane's sequence is bound to; null for media tracks.</param>
    /// <param name="muted">Whether the track starts muted.</param>
    /// <param name="functions">What plays on the track, in time order.</param>
    public XElement Track(int index, string name, int? sceneId, bool muted, IReadOnlyList<QlcShowFunction> functions)
    {
        var track = new XElement(Ns + "Track", new XAttribute("ID", index), new XAttribute("Name", name));
        if (sceneId is { } scene)
            track.SetAttributeValue("SceneID", scene);
        track.SetAttributeValue("isMute", muted ? 1 : 0);
        foreach (var (id, start, duration, color) in functions)
            track.Add(new XElement(Ns + "ShowFunction", new XAttribute("ID", id), new XAttribute("StartTime", start), new XAttribute("Duration", duration), new XAttribute("Color", color)));
        return track;
    }

    /// <summary>Seconds as whole milliseconds, rounded.</summary>
    public static int Ms(double seconds) => (int)Math.Round(seconds * 1000);
}
