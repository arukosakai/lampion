using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Lampion.Core.Export;

/// <summary>
/// The QLC+ workspace document being written: a new one or a loaded base project, with its Engine element
/// guaranteed. Knows QLC+'s structural rules (doctype, element order, id numbering) and its serialized form.
/// </summary>
internal sealed class QlcWorkspace
{
    private QlcWorkspace(XDocument document)
    {
        Document = document;
        var root = document.Root!;
        Ns = root.Name.Namespace;
        Engine = root.Element(Ns + "Engine") ?? AddEngine(root);
    }

    /// <summary>The whole document.</summary>
    public XDocument Document { get; }

    /// <summary>The namespace the document uses (QLC+'s, or whatever the base project declares).</summary>
    public XNamespace Ns { get; }

    /// <summary>The Engine element: InputOutputMap, then fixtures, then functions, then the monitor.</summary>
    public XElement Engine { get; }

    /// <summary>An empty QLC+ 4.14 workspace that opens on the Show Manager.</summary>
    public static QlcWorkspace Create()
    {
        XNamespace ns = QlcExporter.Namespace;
        return new QlcWorkspace(
            new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XDocumentType("Workspace", null, null, null),
                new XElement(
                    ns + "Workspace",
                    new XAttribute("CurrentWindow", "ShowManager"),
                    new XElement(ns + "Creator", new XElement(ns + "Name", "Q Light Controller Plus"), new XElement(ns + "Version", "4.14.4"), new XElement(ns + "Author", "Lampion")),
                    new XElement(ns + "Engine")
                )
            )
        );
    }

    /// <summary>Loads an existing .qxw to add a show to.</summary>
    /// <exception cref="InvalidDataException">The file is not valid XML or not a QLC+ workspace.</exception>
    public static QlcWorkspace Load(string path)
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
        return new QlcWorkspace(doc);
    }

    private XElement AddEngine(XElement root)
    {
        var engine = new XElement(Ns + "Engine");
        if (root.Element(Ns + "Creator") is { } creator)
            creator.AddAfterSelf(engine);
        else
            root.AddFirst(engine);
        return engine;
    }

    /// <summary>Makes sure universes 0..<paramref name="count"/>-1 (QLC+ numbering) exist, creating the InputOutputMap if needed.</summary>
    public void EnsureUniverses(int count)
    {
        var io = Engine.Element(Ns + "InputOutputMap");
        if (io is null)
        {
            io = new XElement(Ns + "InputOutputMap", new XElement(Ns + "BeatGenerator", new XAttribute("BeatType", "Disabled"), new XAttribute("BPM", 0)));
            Engine.AddFirst(io);
        }
        var existing = io.Elements(Ns + "Universe").Select(u => IntOr(u.Attribute("ID")?.Value, -1)).DefaultIfEmpty(-1).Max();
        var anchor = io.Elements(Ns + "Universe").LastOrDefault() ?? io.Elements(Ns + "BeatGenerator").LastOrDefault();
        var missing = new List<XElement>();
        for (var id = existing + 1; id < count; id++)
            missing.Add(new XElement(Ns + "Universe", new XAttribute("Name", $"Universe {id + 1}"), new XAttribute("ID", id)));
        InsertAfter(io, anchor, missing);
    }

    /// <summary>Appends new fixture elements after the existing ones (or after the InputOutputMap).</summary>
    public void AddFixtures(IReadOnlyCollection<XElement> fixtures) =>
        InsertAfter(Engine, Engine.Elements(Ns + "Fixture").LastOrDefault() ?? Engine.Element(Ns + "InputOutputMap"), fixtures);

    /// <summary>Appends functions after the last function-like element, keeping QLC+'s element order.</summary>
    public void AddFunctions(IReadOnlyCollection<XElement> functions)
    {
        XElement? anchor = null;
        foreach (var name in new[] { "Function", "Palette", "ChannelsGroup", "FixtureGroup", "Fixture", "InputOutputMap" })
            if (Engine.Elements(Ns + name).LastOrDefault() is { } last)
            {
                anchor = last;
                break;
            }
        InsertAfter(Engine, anchor, functions);
    }

    /// <summary>The first function id not used by the workspace.</summary>
    public int NextFunctionId() => Engine.Elements(Ns + "Function").Select(f => IntOr(f.Attribute("ID")?.Value, -1)).DefaultIfEmpty(-1).Max() + 1;

    /// <summary>
    /// <paramref name="name"/> (or "Lampion show" when blank), numbered "(2)", "(3)"… if the workspace already
    /// has a show called that, with a warning.
    /// </summary>
    public string UniqueShowName(string name, List<string> warnings)
    {
        name = string.IsNullOrWhiteSpace(name) ? "Lampion show" : name;
        var taken = Engine.Elements(Ns + "Function").Where(f => (string?)f.Attribute("Type") == "Show").Select(f => (string?)f.Attribute("Name")).ToHashSet();
        if (!taken.Contains(name))
            return name;
        for (var n = 2; ; n++)
            if (!taken.Contains($"{name} ({n})"))
            {
                warnings.Add($"The base project already has a show called \"{name}\"; this one is \"{name} ({n})\".");
                return $"{name} ({n})";
            }
    }

    /// <summary>The document as QLC+ itself writes it: one-space indents, LF newlines, UTF-8 without BOM.</summary>
    public string Serialize()
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
            Document.Save(writer);
        // XmlWriter pads an id-less doctype and lowercases the encoding; match what QLC+ itself writes.
        return Encoding.UTF8.GetString(stream.ToArray()).Replace("<!DOCTYPE Workspace >", "<!DOCTYPE Workspace>").Replace("encoding=\"utf-8\"", "encoding=\"UTF-8\"");
    }

    /// <summary>Parses an invariant-culture integer, or returns <paramref name="fallback"/>.</summary>
    public static int IntOr(string? value, int fallback) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;

    private static void InsertAfter(XElement parent, XElement? anchor, IReadOnlyCollection<XElement> nodes)
    {
        if (nodes.Count == 0)
            return;
        if (anchor is null)
            parent.AddFirst(nodes);
        else
            anchor.AddAfterSelf(nodes);
    }
}
