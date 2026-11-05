using System.Text.Json;
using Lampion.Core.Model;

namespace Lampion.Core.Persistence;

public static class ProjectSerializer
{
    public const string Extension = ".lampion";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        AllowOutOfOrderMetadataProperties = true,
    };

    private static readonly JsonSerializerOptions Compact = new(Options) { WriteIndented = false };

    public static string Serialize(Project project, bool indented = true) => JsonSerializer.Serialize(project, indented ? Options : Compact);

    public static Project Deserialize(string json)
    {
        var project = JsonSerializer.Deserialize<Project>(json, Options) ?? throw new InvalidDataException("The project file is empty.");
        Normalize(project);
        return project;
    }

    /// <summary>Saves with media paths relative to the project file, so a show folder can be moved as a whole.</summary>
    public static void Save(Project project, string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        var copy = Deserialize(Serialize(project, indented: false));
        foreach (var clip in copy.AllClips.OfType<MediaClip>())
            clip.Path = RelativeIfInside(directory, clip.Path);
        var temp = path + ".tmp";
        File.WriteAllText(temp, Serialize(copy));
        File.Move(temp, path, overwrite: true);
    }

    public static Project Load(string path)
    {
        var project = Deserialize(File.ReadAllText(path));
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        foreach (var clip in project.AllClips.OfType<MediaClip>())
            if (!string.IsNullOrEmpty(clip.Path) && !Path.IsPathRooted(clip.Path))
                clip.Path = Path.GetFullPath(Path.Combine(directory, clip.Path));
        return project;
    }

    private static string RelativeIfInside(string directory, string path)
    {
        if (string.IsNullOrEmpty(path) || !Path.IsPathRooted(path))
            return path;
        var relative = Path.GetRelativePath(directory, path);
        return relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative) ? path : relative.Replace('\\', '/');
    }

    /// <summary>Restores invariants the editor relies on (sorted clips and keys) after loading.</summary>
    public static void Normalize(Project project)
    {
        foreach (var track in project.Tracks)
        {
            track.SortClips();
            foreach (var clip in track.Clips.OfType<LightClip>())
                foreach (var lane in clip.Automation)
                    lane.SortKeys();
        }
    }
}
