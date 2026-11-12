using System.Text.Json;
using Lampion.Core.Model;
using Lampion.Core.Model.Live;

namespace Lampion.Core.Persistence;

/// <summary>
/// Saves a project's live console to a <c>.lamplive</c> file and loads one into another project, remapping the
/// fixtures and groups it targets onto that project's patch. Thread-safe; file methods do blocking I/O.
/// </summary>
public static class LiveConsoleSerializer
{
    /// <summary>File extension, including the dot.</summary>
    public const string Extension = ".lamplive";

    /// <summary>
    /// <paramref name="project"/>'s console as a file: a deep copy, with show and photo paths made absolute (resolved
    /// against <paramref name="projectPath"/>, the project's file or null while unsaved) and the targeted fixtures and
    /// groups described so they can be found again elsewhere.
    /// </summary>
    public static LiveConsoleFile Export(Project project, string? projectPath)
    {
        var file = new LiveConsoleFile();
        CopyInto(project.Live, file.Console);
        foreach (var show in file.Console.Shows)
            show.Path = ShowPaths.Resolve(projectPath, show.Path);
        foreach (var widget in file.Console.Widgets)
            widget.ImagePath = ShowPaths.Resolve(projectPath, widget.ImagePath);

        var fixtureIds = new HashSet<string>();
        var groupIds = new HashSet<string>();
        foreach (var fader in file.Console.Faders)
        {
            fixtureIds.UnionWith(fader.FixtureIds);
            groupIds.UnionWith(fader.GroupIds);
        }
        foreach (var pad in file.Console.Pads)
        {
            fixtureIds.UnionWith(pad.FixtureIds);
            groupIds.UnionWith(pad.GroupIds);
        }
        foreach (var fixture in project.Fixtures)
            if (fixtureIds.Remove(fixture.Id))
                file.Fixtures.Add(new LiveFixtureRef(fixture.Id, fixture.Name, fixture.Universe, fixture.Address));
        foreach (var group in project.Groups)
            if (groupIds.Remove(group.Id))
                file.Groups.Add(new LiveGroupRef(group.Id, group.Name));
        return file;
    }

    /// <summary>
    /// <paramref name="file"/>'s console remapped onto <paramref name="project"/>: fixtures by id, then by universe and
    /// address; groups by id, then by name (ignoring case). Targets with no match are dropped and listed in the
    /// result. Paths are made relative to <paramref name="projectPath"/> when they are next to it. The project
    /// isn't changed; the caller replaces its console with the result.
    /// </summary>
    public static LiveConsoleImport Import(LiveConsoleFile file, Project project, string? projectPath)
    {
        var console = new LiveSet();
        CopyInto(file.Console, console);
        foreach (var show in console.Shows)
            show.Path = ShowPaths.Relative(projectPath, show.Path);
        foreach (var widget in console.Widgets)
            widget.ImagePath = ShowPaths.Relative(projectPath, widget.ImagePath);

        var fixtureMap = MapFixtures(file, project, out var unmatchedFixtures);
        var groupMap = MapGroups(file, project, out var unmatchedGroups);
        foreach (var fader in console.Faders)
            Remap(fader.FixtureIds, fader.GroupIds, fixtureMap, groupMap);
        foreach (var pad in console.Pads)
            Remap(pad.FixtureIds, pad.GroupIds, fixtureMap, groupMap);
        return new LiveConsoleImport(console, unmatchedFixtures, unmatchedGroups);
    }

    /// <summary>The file as indented JSON.</summary>
    public static string Serialize(LiveConsoleFile file) => JsonSerializer.Serialize(file, LampionJson.Indented);

    /// <summary>A console file from JSON.</summary>
    /// <exception cref="JsonException">The text is not a console file.</exception>
    /// <exception cref="InvalidDataException">The text is empty or damaged.</exception>
    public static LiveConsoleFile Deserialize(string json) => LampionJson.Read<LiveConsoleFile>(json, "The console file is empty.");

    /// <summary>Writes <paramref name="project"/>'s console to <paramref name="path"/>: a temp file first, then replaced.</summary>
    /// <exception cref="IOException">The file can't be written.</exception>
    public static void Save(Project project, string? projectPath, string path)
    {
        var temp = path + ".tmp";
        File.WriteAllText(temp, Serialize(Export(project, projectPath)));
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>Reads a console file.</summary>
    /// <exception cref="IOException">The file can't be read.</exception>
    /// <exception cref="JsonException">The file is not a console file.</exception>
    /// <exception cref="InvalidDataException">The file is empty or damaged.</exception>
    public static LiveConsoleFile Load(string path) => Deserialize(File.ReadAllText(path));

    // A deep copy through JSON, so nothing in the copy is shared with the original.
    private static void CopyInto(LiveSet from, LiveSet to)
    {
        var copy = JsonSerializer.Deserialize<LiveSet>(JsonSerializer.Serialize(from, LampionJson.Compact), LampionJson.Compact)!;
        to.Shows.AddRange(copy.Shows);
        to.Faders.AddRange(copy.Faders);
        to.Pads.AddRange(copy.Pads);
        to.Widgets.AddRange(copy.Widgets);
        to.LightDelayMs = copy.LightDelayMs;
    }

    private static Dictionary<string, string> MapFixtures(LiveConsoleFile file, Project project, out List<string> unmatched)
    {
        var byId = new HashSet<string>();
        var byAddress = new Dictionary<(int, int), string>();
        foreach (var fixture in project.Fixtures)
        {
            byId.Add(fixture.Id);
            byAddress.TryAdd((fixture.Universe, fixture.Address), fixture.Id);
        }
        var map = new Dictionary<string, string>();
        unmatched = [];
        foreach (var reference in file.Fixtures)
            if (byId.Contains(reference.Id))
                map[reference.Id] = reference.Id;
            else if (byAddress.TryGetValue((reference.Universe, reference.Address), out var id))
                map[reference.Id] = id;
            else
                unmatched.Add(reference.Name);
        return map;
    }

    private static Dictionary<string, string> MapGroups(LiveConsoleFile file, Project project, out List<string> unmatched)
    {
        var map = new Dictionary<string, string>();
        unmatched = [];
        foreach (var reference in file.Groups)
        {
            var group = project.Groups.Find(g => g.Id == reference.Id)
                ?? project.Groups.Find(g => string.Equals(g.Name, reference.Name, StringComparison.OrdinalIgnoreCase));
            if (group is not null)
                map[reference.Id] = group.Id;
            else
                unmatched.Add(reference.Name);
        }
        return map;
    }

    private static void Remap(List<string> fixtureIds, List<string> groupIds, Dictionary<string, string> fixtures, Dictionary<string, string> groups)
    {
        Replace(fixtureIds, fixtures);
        Replace(groupIds, groups);
    }

    private static void Replace(List<string> ids, Dictionary<string, string> map)
    {
        var mapped = new List<string>(ids.Count);
        foreach (var id in ids)
            if (map.TryGetValue(id, out var target) && !mapped.Contains(target))
                mapped.Add(target);
        ids.Clear();
        ids.AddRange(mapped);
    }
}
