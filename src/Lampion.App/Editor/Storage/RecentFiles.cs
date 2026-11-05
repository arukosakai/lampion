using System.Text.Json;

namespace Lampion.App.Editor.Storage;

/// <summary>The most recently opened or saved shows, newest first. Never throws: the list is only a convenience.</summary>
internal static class RecentFiles
{
    /// <summary>Entries kept in the file.</summary>
    public const int Capacity = 12;

    /// <summary>The recent shows, newest first; empty when the file is missing or unreadable. Paths may no longer exist.</summary>
    public static List<string> Load()
    {
        try
        {
            return File.Exists(UserPaths.Recent) ? JsonSerializer.Deserialize<List<string>>(File.ReadAllText(UserPaths.Recent)) ?? [] : [];
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>Moves <paramref name="path"/> (made absolute) to the top of the list.</summary>
    public static void Add(string path) => Write([Path.GetFullPath(path), .. Load().Where(p => !UserPaths.Same(p, path))]);

    /// <summary>Takes <paramref name="path"/> off the list.</summary>
    public static void Remove(string path) => Write(Load().Where(p => !UserPaths.Same(p, path)).ToList());

    private static void Write(List<string> paths)
    {
        try
        {
            Directory.CreateDirectory(UserPaths.Root);
            File.WriteAllText(UserPaths.Recent, JsonSerializer.Serialize(paths.Take(Capacity).ToList()));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A recent list that can't be saved only costs convenience; never block opening or saving a show on it.
        }
    }
}
