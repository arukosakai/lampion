using Lampion.App.Editor.Storage;
using Lampion.Core.Persistence;

namespace Lampion.App.Editor;

/// <summary>
/// Per-user files: settings, recent projects, effect presets, saved stage templates and the default layout
/// for new shows. A facade over <see cref="SettingsStore"/>, <see cref="RecentFiles"/>, <see cref="LayoutStore"/>
/// and <see cref="PresetStore"/>; members that return data never throw
/// on a missing or broken file, members that save say what they throw.
/// </summary>
internal static class UserData
{
    /// <summary>The per-user Lampion folder. Not guaranteed to exist yet.</summary>
    public static string Root => UserPaths.Root;

    /// <summary>The stage and rig new shows start from, when the user has set one.</summary>
    public static string DefaultLayoutPath => UserPaths.DefaultLayout;

    /// <summary>The folder of saved stage layout templates.</summary>
    public static string TemplatesDirectory => UserPaths.Templates;

    /// <summary>The folder of saved effect presets.</summary>
    public static string PresetsDirectory => UserPaths.Presets;

    // ---- Settings --------------------------------------------------------------------------------------

    /// <summary>Machine-level preferences; defaults when the file is missing or unreadable.</summary>
    public static UserSettings LoadSettings() => SettingsStore.Load();

    /// <summary>Writes the preferences; failures are ignored.</summary>
    public static void SaveSettings(UserSettings settings) => SettingsStore.Save(settings);

    // ---- Recent files ----------------------------------------------------------------------------------

    /// <summary>Recently opened or saved shows, newest first; the files may no longer exist.</summary>
    public static List<string> RecentProjects() => RecentFiles.Load();

    /// <summary>Moves <paramref name="path"/> to the top of the recent shows. Never throws.</summary>
    public static void AddRecent(string path) => RecentFiles.Add(path);

    /// <summary>Takes <paramref name="path"/> off the recent shows. Never throws.</summary>
    public static void RemoveRecent(string path) => RecentFiles.Remove(path);

    // ---- Stage layouts ---------------------------------------------------------------------------------

    /// <summary>The user's saved templates; files that fail to load are skipped.</summary>
    public static List<(string Path, StageLayout Layout)> Templates() => LayoutStore.Templates();

    /// <summary>Renames <paramref name="layout"/> and saves it as a template; returns the file written.</summary>
    /// <exception cref="IOException">The file could not be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder is not writable.</exception>
    public static string SaveTemplate(StageLayout layout, string name) => LayoutStore.SaveTemplate(layout, name);

    /// <summary>The default layout for new shows, or null when none is set or it can't be read.</summary>
    public static StageLayout? DefaultLayout() => LayoutStore.Default();

    /// <summary>Makes <paramref name="layout"/> the default for new shows.</summary>
    /// <exception cref="IOException">The file could not be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder is not writable.</exception>
    public static void SaveDefaultLayout(StageLayout layout) => LayoutStore.SaveDefault(layout);

    /// <summary>Removes the default layout, so new shows start with an empty rig.</summary>
    /// <exception cref="IOException">The file is in use.</exception>
    /// <exception cref="UnauthorizedAccessException">The file is read-only.</exception>
    public static void ForgetDefaultLayout() => LayoutStore.ForgetDefault();

    /// <summary>The layout in <paramref name="path"/>, or null when it can't be read.</summary>
    public static StageLayout? TryLoadLayout(string path) => LayoutStore.TryLoad(path);

    // ---- Effect presets --------------------------------------------------------------------------------

    /// <summary>The user's saved effect presets, by name; files that fail to load are skipped.</summary>
    public static List<(string Path, EffectPreset Preset)> Presets() => PresetStore.All();

    /// <summary>Raised after a preset is saved or deleted, so every list of presets can refresh.</summary>
    public static event Action? PresetsChanged
    {
        add => PresetStore.Changed += value;
        remove => PresetStore.Changed -= value;
    }

    /// <summary>The file a preset called <paramref name="name"/> is saved to. The file may not exist.</summary>
    public static string PresetPath(string name) => PresetStore.PathFor(name);

    /// <summary>Saves under the preset's name, replacing an earlier preset of the same name; returns the file written.</summary>
    /// <exception cref="IOException">The file could not be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder is not writable.</exception>
    public static string SavePreset(EffectPreset preset) => PresetStore.Save(preset);

    /// <summary>Deletes a preset file and raises <see cref="PresetsChanged"/>.</summary>
    /// <exception cref="IOException">The file is in use.</exception>
    /// <exception cref="UnauthorizedAccessException">The file is read-only or not ours.</exception>
    public static void DeletePreset(string path) => PresetStore.Delete(path);

    /// <summary>The preset in <paramref name="path"/>, or null when it can't be read.</summary>
    public static EffectPreset? TryLoadPreset(string path) => PresetStore.TryLoad(path);
}
