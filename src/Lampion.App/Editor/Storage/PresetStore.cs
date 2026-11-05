using System.Text.Json;
using Lampion.Core.Persistence;

namespace Lampion.App.Editor.Storage;

/// <summary>The user's effect presets, one <see cref="EffectPreset.Extension"/> file per preset, named after it.</summary>
internal static class PresetStore
{
    /// <summary>Raised after a preset is saved or deleted, on the thread that did it.</summary>
    public static event Action? Changed;

    /// <summary>The saved presets ordered by name (current culture, ignoring case); files that fail to load are skipped.</summary>
    public static List<(string Path, EffectPreset Preset)> All()
    {
        if (!Directory.Exists(UserPaths.Presets))
            return [];
        var result = new List<(string, EffectPreset)>();
        foreach (var file in Directory.EnumerateFiles(UserPaths.Presets, "*" + EffectPreset.Extension))
            if (TryLoad(file) is { } preset)
                result.Add((file, preset));
        return result.OrderBy(p => p.Item2.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>The file a preset called <paramref name="name"/> is saved to. The file may not exist.</summary>
    public static string PathFor(string name) => Path.Combine(UserPaths.Presets, UserPaths.SafeFileName(name, "Preset") + EffectPreset.Extension);

    /// <summary>Saves under the preset's name, replacing an earlier preset of the same name, and raises <see cref="Changed"/>.</summary>
    /// <returns>The file written.</returns>
    /// <exception cref="IOException">The file could not be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder is not writable.</exception>
    public static string Save(EffectPreset preset)
    {
        Directory.CreateDirectory(UserPaths.Presets);
        var path = PathFor(preset.Name);
        preset.Save(path);
        Changed?.Invoke();
        return path;
    }

    /// <summary>Deletes the preset file and raises <see cref="Changed"/>.</summary>
    /// <exception cref="IOException">The file is in use.</exception>
    /// <exception cref="UnauthorizedAccessException">The file is read-only or not ours.</exception>
    public static void Delete(string path)
    {
        File.Delete(path);
        Changed?.Invoke();
    }

    /// <summary>The preset in <paramref name="path"/>, or null when it can't be read.</summary>
    public static EffectPreset? TryLoad(string path)
    {
        try
        {
            return EffectPreset.Load(path);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }
}
