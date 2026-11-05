using System.Text.Json;
using Lampion.Core.Persistence;

namespace Lampion.App.Editor.Storage;

/// <summary>Stage layouts kept for the user: saved templates and the default layout new shows start from.</summary>
internal static class LayoutStore
{
    /// <summary>The saved templates in file-name order; files that fail to load are skipped.</summary>
    public static List<(string Path, StageLayout Layout)> Templates()
    {
        if (!Directory.Exists(UserPaths.Templates))
            return [];
        var result = new List<(string, StageLayout)>();
        foreach (var file in Directory.EnumerateFiles(UserPaths.Templates, "*" + StageLayout.Extension).Order())
            if (TryLoad(file) is { } layout)
                result.Add((file, layout));
        return result;
    }

    /// <summary>Renames <paramref name="layout"/> to <paramref name="name"/> and saves it as a template, replacing one of the same name.</summary>
    /// <returns>The file written.</returns>
    /// <exception cref="IOException">The file could not be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder is not writable.</exception>
    public static string SaveTemplate(StageLayout layout, string name)
    {
        Directory.CreateDirectory(UserPaths.Templates);
        var path = Path.Combine(UserPaths.Templates, UserPaths.SafeFileName(name, "Template") + StageLayout.Extension);
        layout.Name = name;
        layout.Save(path);
        return path;
    }

    /// <summary>The default layout, or null when none is set or it can't be read.</summary>
    public static StageLayout? Default() => File.Exists(UserPaths.DefaultLayout) ? TryLoad(UserPaths.DefaultLayout) : null;

    /// <summary>Makes <paramref name="layout"/> the default for new shows.</summary>
    /// <exception cref="IOException">The file could not be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder is not writable.</exception>
    public static void SaveDefault(StageLayout layout)
    {
        Directory.CreateDirectory(UserPaths.Root);
        layout.Save(UserPaths.DefaultLayout);
    }

    /// <summary>Removes the default layout, if there is one.</summary>
    /// <exception cref="IOException">The file is in use.</exception>
    /// <exception cref="UnauthorizedAccessException">The file is read-only.</exception>
    public static void ForgetDefault()
    {
        if (File.Exists(UserPaths.DefaultLayout))
            File.Delete(UserPaths.DefaultLayout);
    }

    /// <summary>The layout in <paramref name="path"/>, or null when it can't be read.</summary>
    public static StageLayout? TryLoad(string path)
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
