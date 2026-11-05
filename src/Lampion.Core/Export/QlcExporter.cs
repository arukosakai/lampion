using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Lampion.Core.Engine;
using Lampion.Core.Model;

namespace Lampion.Core.Export;

public sealed class QlcExportOptions
{
    /// <summary>How often the show is sampled before being reduced to linear fades.</summary>
    public double SampleRate { get; set; } = 25;

    /// <summary>Largest DMX error a fade may introduce in place of the sampled values.</summary>
    public int Tolerance { get; set; } = 1;

    /// <summary>A QLC+ project to add the show to; null builds a new workspace.</summary>
    public string? BaseWorkspacePath { get; set; }

    /// <summary>Folder the .qxw will be saved in; media paths are written relative to it when possible.</summary>
    public string? OutputDirectory { get; set; }

    /// <summary>Folder the Lampion project lives in, to resolve relative media paths.</summary>
    public string? ProjectDirectory { get; set; }
}

public sealed record QlcExportResult(string Xml, IReadOnlyList<string> Warnings, int FixtureCount, int StepCount);

/// <summary>
/// Writes a QLC+ 4 workspace (.qxw). Each lane — a fixture group, or a lone fixture — becomes a Sequence
/// bound to a hidden Scene, played by one track of a Show alongside the audio and video.
/// </summary>
/// <remarks>
/// QLC+ facts this relies on (4.14 source): <c>&lt;!DOCTYPE Workspace&gt;</c> is mandatory; the Engine
/// lists InputOutputMap, then fixtures, then functions; a step occupies FadeIn + Hold and its text is
/// <c>fixtureId:channel,value,…:fixtureId:…</c> with 0-based channels; a fixture that partly overlaps
/// another in the same universe is dropped on load, so it is left out here with a warning instead.
/// </remarks>
public sealed class QlcExporter
{
    public const string Namespace = "http://www.qlcplus.org/Workspace";

    private readonly Project _project;
    private readonly QlcExportOptions _options;
    private readonly List<string> _warnings = [];
    private XNamespace _ns = Namespace;

    public QlcExporter(Project project, QlcExportOptions? options = null)
    {
        _project = project;
        _options = options ?? new QlcExportOptions();
    }

    public QlcExportResult Export()
    {
        var doc = _options.BaseWorkspacePath is { } basePath ? LoadBase(basePath) : CreateWorkspace();
        var root = doc.Root!;
        _ns = root.Name.Namespace;
        var engine = root.Element(_ns + "Engine") ?? AddEngine(root);

        var patch = PlanPatch(engine);
        EnsureUniverses(engine, patch.Values.Select(p => p.Fixture.Universe).DefaultIfEmpty(1).Max());
        var newFixtureElements = patch.Values.Where(p => p.IsNew).Select(FixtureElement).ToList();
        InsertAfter(engine, engine.Elements(_ns + "Fixture").LastOrDefault() ?? engine.Element(_ns + "InputOutputMap"), newFixtureElements);

        var showName = UniqueShowName(engine, _project.Name);
        var functionIds = engine.Elements(_ns + "Function").Select(f => IntOr(f.Attribute("ID")?.Value, -1)).DefaultIfEmpty(-1).Max() + 1;
        var (functions, steps) = BuildShow(showName, patch, () => functionIds++);
        InsertAfter(engine, FunctionAnchor(engine), functions);

        WriteMonitor(engine, patch.Values.Where(p => p.IsNew).ToList());

        return new QlcExportResult(Serialize(doc), _warnings, patch.Count, steps);
    }

    // ---- Workspace -------------------------------------------------------------------------------------

    private XDocument CreateWorkspace()
    {
        XNamespace ns = Namespace;
        return new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XDocumentType("Workspace", null, null, null),
            new XElement(
                ns + "Workspace",
                new XAttribute("CurrentWindow", "ShowManager"),
                new XElement(ns + "Creator", new XElement(ns + "Name", "Q Light Controller Plus"), new XElement(ns + "Version", "4.14.4"), new XElement(ns + "Author", "Lampion")),
                new XElement(ns + "Engine")
            )
        );
    }

    private static XDocument LoadBase(string path)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Load(path, LoadOptions.None);
        }
        catch (XmlException ex)
        {
            throw new InvalidDataException($"The base project is not valid XML: {ex.Message}", ex);
        }
        if (doc.Root?.Name.LocalName != "Workspace")
            throw new InvalidDataException("The base file is not a QLC+ project (no Workspace element).");
        // QLC+ refuses a workspace without its doctype.
        if (doc.DocumentType is null)
            doc.Root.AddBeforeSelf(new XDocumentType("Workspace", null, null, null));
        return doc;
    }

    private XElement AddEngine(XElement root)
    {
        var engine = new XElement(_ns + "Engine");
        if (root.Element(_ns + "Creator") is { } creator)
            creator.AddAfterSelf(engine);
        else
            root.AddFirst(engine);
        return engine;
    }

    private void EnsureUniverses(XElement engine, int count)
    {
        var io = engine.Element(_ns + "InputOutputMap");
        if (io is null)
        {
            io = new XElement(_ns + "InputOutputMap", new XElement(_ns + "BeatGenerator", new XAttribute("BeatType", "Disabled"), new XAttribute("BPM", 0)));
            engine.AddFirst(io);
        }
        var existing = io.Elements(_ns + "Universe").Select(u => IntOr(u.Attribute("ID")?.Value, -1)).DefaultIfEmpty(-1).Max();
        var anchor = io.Elements(_ns + "Universe").LastOrDefault() ?? io.Elements(_ns + "BeatGenerator").LastOrDefault();
        var missing = new List<XElement>();
        for (var id = existing + 1; id < count; id++)
            missing.Add(new XElement(_ns + "Universe", new XAttribute("Name", $"Universe {id + 1}"), new XAttribute("ID", id)));
        InsertAfter(io, anchor, missing);
    }

    private static void InsertAfter(XElement parent, XElement? anchor, IReadOnlyCollection<XElement> nodes)
    {
        if (nodes.Count == 0)
            return;
        if (anchor is null)
            parent.AddFirst(nodes);
        else
            anchor.AddAfterSelf(nodes);
    }

    private XElement? FunctionAnchor(XElement engine)
    {
        foreach (var name in new[] { "Function", "Palette", "ChannelsGroup", "FixtureGroup", "Fixture", "InputOutputMap" })
            if (engine.Elements(_ns + name).LastOrDefault() is { } last)
                return last;
        return null;
    }

    private static string Serialize(XDocument doc)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = " ",
            Encoding = new UTF8Encoding(false),
            NewLineChars = "\n",
        };
        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, settings))
            doc.Save(writer);
        // XmlWriter pads an id-less doctype and lowercases the encoding; match what QLC+ itself writes.
        return Encoding.UTF8.GetString(stream.ToArray())
            .Replace("<!DOCTYPE Workspace >", "<!DOCTYPE Workspace>")
            .Replace("encoding=\"utf-8\"", "encoding=\"UTF-8\"");
    }

    // ---- Patch -----------------------------------------------------------------------------------------

    private sealed record PatchedFixture(Fixture Fixture, FixtureProfile Profile, int QlcId, bool IsNew);

    private sealed record Occupant(string Name, int Universe, int Address, int Channels, int QlcId, bool FromBase);

    /// <summary>
    /// Which QLC+ fixture each Lampion fixture becomes. A base-project fixture at exactly the same universe
    /// and address is reused; one that partly overlaps is a conflict QLC+ would resolve by dropping ours.
    /// </summary>
    private Dictionary<string, PatchedFixture> PlanPatch(XElement engine)
    {
        var occupants = engine
            .Elements(_ns + "Fixture")
            .Select(f => new Occupant(
                (string?)f.Element(_ns + "Name") ?? "?",
                IntOr((string?)f.Element(_ns + "Universe"), 0) + 1,
                IntOr((string?)f.Element(_ns + "Address"), 0) + 1,
                Math.Max(1, IntOr((string?)f.Element(_ns + "Channels"), 1)),
                IntOr((string?)f.Element(_ns + "ID"), -1),
                true
            ))
            .Where(o => o.QlcId >= 0)
            .ToList();
        var nextId = occupants.Select(o => o.QlcId).DefaultIfEmpty(-1).Max() + 1;
        var result = new Dictionary<string, PatchedFixture>();

        foreach (var fixture in _project.Fixtures)
        {
            if (_project.ProfileOf(fixture) is not { } profile || profile.Channels.Count == 0)
            {
                _warnings.Add($"\"{fixture.Name}\" has no fixture profile and was left out.");
                continue;
            }
            var count = profile.Channels.Count;
            if (fixture.Address < 1 || fixture.Address + count - 1 > 512 || fixture.Universe < 1)
            {
                _warnings.Add($"\"{fixture.Name}\" ({Range(fixture.Universe, fixture.Address, count)}) does not fit in a universe and was left out.");
                continue;
            }
            var exact = occupants.Find(o => o.Universe == fixture.Universe && o.Address == fixture.Address);
            if (exact is not null)
            {
                if (exact.Channels != count)
                    _warnings.Add($"\"{fixture.Name}\" reuses \"{exact.Name}\" at {Range(fixture.Universe, fixture.Address, count)}, which has {exact.Channels} channels instead of {count}.");
                result[fixture.Id] = new PatchedFixture(fixture, profile, exact.QlcId, IsNew: false);
                continue;
            }
            var overlap = occupants.Find(o => o.Universe == fixture.Universe && o.Address <= fixture.Address + count - 1 && fixture.Address <= o.Address + o.Channels - 1);
            if (overlap is not null)
            {
                _warnings.Add(
                    $"\"{fixture.Name}\" ({Range(fixture.Universe, fixture.Address, count)}) overlaps \"{overlap.Name}\" ({Range(overlap.Universe, overlap.Address, overlap.Channels)}); QLC+ would not load it, so it was left out."
                );
                continue;
            }
            var id = nextId++;
            occupants.Add(new Occupant(fixture.Name, fixture.Universe, fixture.Address, count, id, false));
            result[fixture.Id] = new PatchedFixture(fixture, profile, id, IsNew: true);
        }
        return result;
    }

    private static string Range(int universe, int address, int channels) => $"U{universe} {address}-{address + channels - 1}";

    /// <summary>
    /// QLC+'s built-in RGB panel mode when the channels are exactly R, G, B in some order, or R, G, B, W —
    /// the monitor then mixes the colour. Anything else becomes a Generic dimmer pack.
    /// </summary>
    internal static string? RgbPanelMode(FixtureProfile profile)
    {
        var roles = profile.Channels.Select(c => c.Role).ToList();
        if (roles.Count == 3 && roles.Contains(ChannelRole.Red) && roles.Contains(ChannelRole.Green) && roles.Contains(ChannelRole.Blue))
            return string.Concat(roles.Select(r => r.ToString()[0]));
        if (roles.SequenceEqual([ChannelRole.Red, ChannelRole.Green, ChannelRole.Blue, ChannelRole.White]))
            return "RGBW";
        return null;
    }

    private XElement FixtureElement(PatchedFixture patched)
    {
        var element = new XElement(_ns + "Fixture", new XElement(_ns + "Manufacturer", "Generic"));
        if (RgbPanelMode(patched.Profile) is { } mode)
            element.Add(new XElement(_ns + "Model", "RGBPanel"), new XElement(_ns + "Mode", mode), new XElement(_ns + "Width", 300), new XElement(_ns + "Height", 300));
        else
            element.Add(new XElement(_ns + "Model", "Generic"), new XElement(_ns + "Mode", $"{patched.Profile.Channels.Count} Channel"));
        element.Add(
            new XElement(_ns + "ID", patched.QlcId),
            new XElement(_ns + "Name", patched.Fixture.Name),
            new XElement(_ns + "Universe", patched.Fixture.Universe - 1),
            new XElement(_ns + "Address", patched.Fixture.Address - 1),
            new XElement(_ns + "Channels", patched.Profile.Channels.Count)
        );
        return element;
    }

    // ---- Show ------------------------------------------------------------------------------------------

    private sealed record Lane(string Name, string Color, List<PatchedFixture> Fixtures);

    /// <summary>Fixtures grouped the way the show's tracks will be: by first group, then one lane per loose fixture.</summary>
    private List<Lane> PlanLanes(Dictionary<string, PatchedFixture> patch)
    {
        var lanes = new List<Lane>();
        var placed = new HashSet<int>();
        foreach (var group in _project.Groups)
        {
            var members = group.FixtureIds.Where(patch.ContainsKey).Select(id => patch[id]).Where(p => placed.Add(p.QlcId)).ToList();
            if (members.Count > 0)
                lanes.Add(new Lane(group.Name, group.Color, members));
        }
        foreach (var patched in patch.Values)
            if (placed.Add(patched.QlcId))
                lanes.Add(new Lane(patched.Fixture.Name, "#5A7DBF", [patched]));
        return lanes;
    }

    private string UniqueShowName(XElement engine, string name)
    {
        name = string.IsNullOrWhiteSpace(name) ? "Lampion show" : name;
        var taken = engine.Elements(_ns + "Function").Where(f => (string?)f.Attribute("Type") == "Show").Select(f => (string?)f.Attribute("Name")).ToHashSet();
        if (!taken.Contains(name))
            return name;
        for (var n = 2; ; n++)
            if (!taken.Contains($"{name} ({n})"))
            {
                _warnings.Add($"The base project already has a show called \"{name}\"; this one is \"{name} ({n})\".");
                return $"{name} ({n})";
            }
    }

    private (List<XElement> Functions, int Steps) BuildShow(string showName, Dictionary<string, PatchedFixture> patch, Func<int> nextId)
    {
        var functions = new List<XElement>();
        var tracks = new List<XElement>();
        var showMs = Ms(_project.ContentEnd);
        var path = showName.Replace('/', '-');
        var totalSteps = 0;

        var lanes = PlanLanes(patch);
        var samples = SampleShow(lanes.SelectMany(l => l.Fixtures).ToList(), out var times);
        foreach (var lane in lanes)
        {
            var sceneId = nextId();
            var scene = Function(sceneId, "Scene", $"{showName} - {lane.Name} (scene)", path);
            scene.SetAttributeValue("Hidden", "True");
            foreach (var f in lane.Fixtures)
                scene.Add(new XElement(_ns + "FixtureVal", new XAttribute("ID", f.QlcId), string.Join(",", Enumerable.Range(0, f.Profile.Channels.Count).Select(c => $"{c},0"))));
            functions.Add(scene);

            var sequenceId = nextId();
            var sequence = Function(sequenceId, "Sequence", $"{showName} - {lane.Name}", path);
            sequence.SetAttributeValue("BoundScene", sceneId);
            sequence.Add(
                new XElement(_ns + "Direction", "Forward"),
                new XElement(_ns + "RunOrder", "SingleShot"),
                new XElement(_ns + "SpeedModes", new XAttribute("FadeIn", "PerStep"), new XAttribute("FadeOut", "PerStep"), new XAttribute("Duration", "PerStep"))
            );
            var vectors = samples.Select(frame => lane.Fixtures.SelectMany(f => frame[f.Fixture.Id]).ToArray()).ToList();
            var keep = Simplify(vectors, times, _options.Tolerance);
            var previousMs = 0;
            for (var s = 0; s < keep.Count; s++)
            {
                var k = keep[s];
                var atMs = Ms(times[k]);
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
                        text.Append(c.ToString(CultureInfo.InvariantCulture)).Append(',').Append(vectors[k][offset + c].ToString(CultureInfo.InvariantCulture));
                    }
                    offset += count;
                }
                // The last step holds to the end of the show; every other step is a linear fade from the previous one.
                var hold = s == keep.Count - 1 ? Math.Max(0, showMs - atMs) : 0;
                sequence.Add(
                    new XElement(
                        _ns + "Step",
                        new XAttribute("Number", s),
                        new XAttribute("FadeIn", atMs - previousMs),
                        new XAttribute("Hold", hold),
                        new XAttribute("FadeOut", 0),
                        new XAttribute("Values", offset),
                        text.ToString()
                    )
                );
                previousMs = atMs;
            }
            totalSteps += keep.Count;
            functions.Add(sequence);
            tracks.Add(Track(tracks.Count, lane.Name, sceneId, false, (sequenceId, 0, showMs, lane.Color)));
        }

        foreach (var track in _project.Tracks.Where(t => t.Kind != TrackKind.Light))
        {
            var shows = new List<(int Id, int Start, int Duration, string Color)>();
            foreach (var clip in track.Clips.OfType<MediaClip>().OrderBy(c => c.Start))
            {
                var isVideo = track.Kind == TrackKind.Video;
                var id = nextId();
                var function = Function(id, isVideo ? "Video" : "Audio", $"{showName} - {clip.Name}", path);
                function.Add(new XElement(_ns + "RunOrder", "SingleShot"));
                var source = new XElement(_ns + "Source", MediaPath(clip.Path));
                if (isVideo)
                    source.SetAttributeValue("Fullscreen", 1);
                function.Add(source);
                functions.Add(function);
                if (clip.MediaOffset > 0.01)
                    _warnings.Add($"\"{clip.Name}\" is trimmed {clip.MediaOffset:0.##}s into the file; QLC+ 4 always plays media from the start, so trim the file itself.");
                shows.Add((id, Ms(clip.Start), Ms(clip.Duration), isVideo ? "#938C14" : "#608053"));
            }
            if (shows.Count > 0)
                tracks.Add(Track(tracks.Count, track.Name, null, track.Muted, [.. shows]));
        }

        var show = new XElement(_ns + "Function", new XAttribute("ID", nextId()), new XAttribute("Type", "Show"), new XAttribute("Name", showName));
        show.Add(new XElement(_ns + "TimeDivision", new XAttribute("Type", "Time"), new XAttribute("BPM", (int)Math.Round(_project.Bpm))));
        show.Add(tracks);
        functions.Add(show);
        return (functions, totalSteps);
    }

    private XElement Function(int id, string type, string name, string path)
    {
        var element = new XElement(_ns + "Function", new XAttribute("ID", id), new XAttribute("Type", type), new XAttribute("Name", name), new XAttribute("Path", path));
        element.Add(new XElement(_ns + "Speed", new XAttribute("FadeIn", 0), new XAttribute("FadeOut", 0), new XAttribute("Duration", 0)));
        return element;
    }

    private XElement Track(int index, string name, int? sceneId, bool muted, params (int Id, int Start, int Duration, string Color)[] functions)
    {
        var track = new XElement(_ns + "Track", new XAttribute("ID", index), new XAttribute("Name", name));
        if (sceneId is { } scene)
            track.SetAttributeValue("SceneID", scene);
        track.SetAttributeValue("isMute", muted ? 1 : 0);
        foreach (var (id, start, duration, color) in functions)
            track.Add(
                new XElement(_ns + "ShowFunction", new XAttribute("ID", id), new XAttribute("StartTime", start), new XAttribute("Duration", duration), new XAttribute("Color", color))
            );
        return track;
    }

    private string MediaPath(string path)
    {
        var absolute = Path.IsPathRooted(path) || _options.ProjectDirectory is null ? path : Path.GetFullPath(Path.Combine(_options.ProjectDirectory, path));
        if (_options.OutputDirectory is null || !Path.IsPathRooted(absolute))
            return path;
        var relative = Path.GetRelativePath(_options.OutputDirectory, absolute);
        return Path.IsPathRooted(relative) ? absolute : relative.Replace('\\', '/');
    }

    private List<Dictionary<string, byte[]>> SampleShow(List<PatchedFixture> fixtures, out List<double> times)
    {
        var renderer = new ShowRenderer(_project);
        var end = _project.ContentEnd;
        var rate = Math.Clamp(_options.SampleRate, 1, 100);
        var count = (int)Math.Ceiling(end * rate);
        times = [];
        var frames = new List<Dictionary<string, byte[]>>(count + 1);
        for (var k = 0; k <= count; k++)
        {
            var t = Math.Min(end, k / rate);
            times.Add(t);
            frames.Add(fixtures.Count == 0 ? [] : renderer.RenderFixtures(t));
        }
        return frames;
    }

    /// <summary>
    /// Indices of the samples to keep so that a linear fade between consecutive kept samples stays within
    /// <paramref name="tolerance"/> of every sample it skips. Greedy, one pass: for each channel it narrows
    /// the range of slopes from the last kept sample that every skipped sample allows.
    /// </summary>
    internal static List<int> Simplify(IReadOnlyList<byte[]> vectors, IReadOnlyList<double> times, int tolerance)
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

    // ---- Monitor ---------------------------------------------------------------------------------------

    /// <summary>
    /// Places new fixtures in QLC+ 4's 2D fixture monitor, front view: x across, height measured down from
    /// the top of the grid (the monitor's y grows downward). Positions are top-left corners in millimetres.
    /// </summary>
    private void WriteMonitor(XElement engine, List<PatchedFixture> fixtures)
    {
        if (fixtures.Count == 0)
            return;
        const int item = 300;
        double Width(PatchedFixture f) => RgbPanelMode(f.Profile) is null ? item * f.Profile.Channels.Count : item;
        var left = fixtures.Min(f => f.Fixture.Position.X * 1000 - Width(f) / 2);
        var right = fixtures.Max(f => f.Fixture.Position.X * 1000 + Width(f) / 2);
        var top = Math.Max(0, fixtures.Max(f => f.Fixture.Position.Y * 1000 + item / 2.0));
        var monitor = engine.Element(_ns + "Monitor");
        var grid = monitor?.Element(_ns + "Grid");
        var existingWidth = IntOr((string?)grid?.Attribute("Width"), 0) * 1000;
        var existingHeight = IntOr((string?)grid?.Attribute("Depth") ?? (string?)grid?.Attribute("Height"), 0) * 1000;
        var gridWidth = Math.Max(existingWidth, Math.Ceiling(Math.Max(5000, right - left + 2000) / 1000) * 1000);
        var gridHeight = Math.Max(existingHeight, Math.Ceiling(Math.Max(3000, top + 1000) / 1000) * 1000);
        var origin = (left + right) / 2 - gridWidth / 2;

        if (monitor is null)
        {
            monitor = new XElement(
                _ns + "Monitor",
                new XAttribute("DisplayMode", 1),
                new XAttribute("ShowLabels", 1),
                new XElement(_ns + "ChannelStyle", 0),
                new XElement(_ns + "ValueStyle", 0)
            );
            engine.Add(monitor);
        }
        if (grid is null)
        {
            grid = new XElement(_ns + "Grid", new XAttribute("Units", 0));
            monitor.Add(grid);
        }
        grid.SetAttributeValue("Width", (int)(gridWidth / 1000));
        grid.SetAttributeValue("Height", (int)(gridHeight / 1000));
        grid.SetAttributeValue("Depth", (int)(gridHeight / 1000));
        var groupColors = _project.Groups.SelectMany(g => g.FixtureIds.Select(id => (id, g.Color))).GroupBy(x => x.id).ToDictionary(x => x.Key, x => x.First().Color);
        foreach (var f in fixtures)
        {
            var element = new XElement(
                _ns + "FxItem",
                new XAttribute("ID", f.QlcId),
                new XAttribute("XPos", (int)Math.Round(f.Fixture.Position.X * 1000 - origin - Width(f) / 2)),
                new XAttribute("YPos", (int)Math.Round(gridHeight - f.Fixture.Position.Y * 1000 - item / 2.0))
            );
            if (RgbPanelMode(f.Profile) is null && groupColors.TryGetValue(f.Fixture.Id, out var gel))
                element.SetAttributeValue("GelColor", gel);
            monitor.Add(element);
        }
    }

    private static int Ms(double seconds) => (int)Math.Round(seconds * 1000);

    private static int IntOr(string? value, int fallback) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
}
