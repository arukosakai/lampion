namespace Lampion.Core.Persistence;

/// <summary>
/// Paths stored in a project relative to its file when they sit next to it (so a show folder can move as a whole),
/// absolute otherwise. Thread-safe.
/// </summary>
public static class ShowPaths
{
    /// <summary>
    /// <paramref name="path"/> relative to <paramref name="projectPath"/>'s folder when it is inside it, with forward
    /// slashes; otherwise unchanged. Empty paths, and any path while the project is unsaved (null), stay as they are.
    /// </summary>
    public static string Relative(string? projectPath, string path)
    {
        if (projectPath is null || path.Length == 0 || !Path.IsPathRooted(path))
            return path;
        var relative = Path.GetRelativePath(Folder(projectPath), path);
        return relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative) ? path : relative.Replace('\\', '/');
    }

    /// <summary>
    /// <paramref name="path"/> as a full path: relative ones are resolved against <paramref name="projectPath"/>'s folder.
    /// Empty paths, and relative ones while the project is unsaved (null), stay as they are.
    /// </summary>
    public static string Resolve(string? projectPath, string path)
    {
        if (string.IsNullOrEmpty(path) || Path.IsPathRooted(path) || projectPath is null)
            return path;
        return Path.GetFullPath(Path.Combine(Folder(projectPath), path));
    }

    private static string Folder(string projectPath) => Path.GetDirectoryName(Path.GetFullPath(projectPath))!;
}
