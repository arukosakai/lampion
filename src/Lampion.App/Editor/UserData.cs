using System.Text.Json;
using Lampion.Core.Model;
using Lampion.Core.Persistence;

namespace Lampion.App.Editor;

/// <summary>Per-user files: recent projects, saved stage templates and the default layout for new shows.</summary>
public static class UserData
{
    private const int MaxRecent = 12;

    public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lampion");

    public static string DefaultLayoutPath => Path.Combine(Root, "default" + StageLayout.Extension);

    public static string TemplatesDirectory => Path.Combine(Root, "Templates");

    private static string RecentPath => Path.Combine(Root, "recent.json");

    public static List<string> RecentProjects()
    {
        try
        {
            return File.Exists(RecentPath) ? JsonSerializer.Deserialize<List<string>>(File.ReadAllText(RecentPath)) ?? [] : [];
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public static void AddRecent(string path) => WriteRecent([Path.GetFullPath(path), .. RecentProjects().Where(p => !SamePath(p, path))]);

    public static void RemoveRecent(string path) => WriteRecent(RecentProjects().Where(p => !SamePath(p, path)).ToList());

    private static void WriteRecent(List<string> paths)
    {
        try
        {
            Directory.CreateDirectory(Root);
            File.WriteAllText(RecentPath, JsonSerializer.Serialize(paths.Take(MaxRecent).ToList()));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A recent list that can't be saved only costs convenience; never block opening or saving a show on it.
        }
    }

    private static bool SamePath(string a, string b) => string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);

    /// <summary>The user's saved templates; files that fail to load are skipped.</summary>
    public static List<(string Path, StageLayout Layout)> Templates()
    {
        if (!Directory.Exists(TemplatesDirectory))
            return [];
        var result = new List<(string, StageLayout)>();
        foreach (var file in Directory.EnumerateFiles(TemplatesDirectory, "*" + StageLayout.Extension).Order())
            if (TryLoadLayout(file) is { } layout)
                result.Add((file, layout));
        return result;
    }

    public static string SaveTemplate(StageLayout layout, string name)
    {
        Directory.CreateDirectory(TemplatesDirectory);
        var safe = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim();
        var path = Path.Combine(TemplatesDirectory, (safe.Length == 0 ? "Template" : safe) + StageLayout.Extension);
        layout.Name = name;
        layout.Save(path);
        return path;
    }

    public static StageLayout? DefaultLayout() => File.Exists(DefaultLayoutPath) ? TryLoadLayout(DefaultLayoutPath) : null;

    public static StageLayout? TryLoadLayout(string path)
    {
        try
        {
            return StageLayout.Load(path);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

/// <summary>Ways to begin a show: blank, from a stage layout, or the demo.</summary>
public static class ProjectFactory
{
    public static Project Empty()
    {
        var project = new Project();
        project.Profiles.AddRange(BuiltInProfiles.All().Select(p => p.Clone(newId: false)));
        project.Tracks.AddRange([new Track { Name = "Lights 1" }, MediaTrack(TrackKind.Audio), MediaTrack(TrackKind.Video)]);
        return project;
    }

    /// <summary>A blank show on a ready venue, with one light track per group so the first clip can go straight on.</summary>
    public static Project FromLayout(StageLayout layout)
    {
        var project = new Project();
        project.Profiles.AddRange(BuiltInProfiles.All().Select(p => p.Clone(newId: false)));
        layout.ApplyTo(project, includeRig: true);
        var tracks = project.Groups.Select(g => new Track { Name = g.Name, GroupIds = [g.Id], Order = FixtureOrder.LeftToRight }).ToList();
        if (tracks.Count == 0)
            tracks.Add(new Track { Name = "Lights 1" });
        project.Tracks.AddRange([.. tracks, MediaTrack(TrackKind.Audio), MediaTrack(TrackKind.Video)]);
        return project;
    }

    public static Project Demo()
    {
        var project = Project.CreateDemo();
        project.Name = "Demo show";
        return project;
    }

    private static Track MediaTrack(TrackKind kind) => new() { Name = kind.ToString(), Kind = kind };
}
