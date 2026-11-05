using System.Text.Json;
using Lampion.Core.Model;
using Lampion.Core.Model.Timeline;

namespace Lampion.Core.Persistence;

/// <summary>Reads and writes <c>.lampion</c> project files. Thread-safe; file methods do blocking I/O.</summary>
public static class ProjectSerializer
{
    /// <summary>File extension, including the dot.</summary>
    public const string Extension = ".lampion";

    /// <summary>The project as JSON, indented for files or compact for undo snapshots and deep copies.</summary>
    public static string Serialize(Project project, bool indented = true) => JsonSerializer.Serialize(project, indented ? LampionJson.Indented : LampionJson.Compact);

    /// <summary>A project from JSON, with clips and keys sorted (see <see cref="Normalize"/>).</summary>
    /// <exception cref="JsonException">The text is not a project.</exception>
    /// <exception cref="InvalidDataException">The text is empty or damaged.</exception>
    public static Project Deserialize(string json)
    {
        var project = LampionJson.Read<Project>(json, "The project file is empty.");
        Normalize(project);
        return project;
    }

    /// <summary>Saves with media paths relative to the project file, so a show folder can be moved as a whole. Writes a temp file first, then replaces.</summary>
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

    /// <summary>Loads a project, resolving relative media paths against the file's folder.</summary>
    /// <exception cref="IOException">The file can't be read.</exception>
    /// <exception cref="JsonException">The file is not a project.</exception>
    /// <exception cref="InvalidDataException">The file is empty or damaged.</exception>
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

    /// <summary>Restores invariants the editor relies on (clips sorted by start, keys by time) after loading.</summary>
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
