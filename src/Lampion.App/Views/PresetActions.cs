using System.Text.Json;
using Avalonia.Controls;
using Lampion.App.Editor;
using Lampion.Core.Model.Timeline;
using Lampion.Core.Persistence;

namespace Lampion.App.Views;

/// <summary>The dialogs around user presets, shared by the Effects panel, the inspector and the timeline.</summary>
internal static class PresetActions
{
    private const string FileTypeName = "Lampion effect preset";
    private static readonly string ExtensionNoDot = EffectPreset.Extension.TrimStart('.');

    /// <summary>
    /// Built-in presets first (no path: they can't be renamed or deleted), then the user's. Reads the user's
    /// preset folder, so call it when the list is (re)built, not per frame. The list is a fresh copy.
    /// </summary>
    public static List<(string? Path, EffectPreset Preset)> Library() =>
        [.. BuiltInPresets.All().Select(p => ((string?)null, p)), .. UserData.Presets().Select(p => ((string?)p.Path, p.Preset))];

    /// <summary>Asks for a name and saves the clip's look (effect, fades, light automation) as a user preset.</summary>
    /// <param name="owner">Window the dialogs belong to.</param>
    /// <param name="clip">The clip to capture; it is not changed.</param>
    public static async Task SaveFromClip(Window owner, LightClip clip)
    {
        var name = await AskName(owner, "Save as preset", clip.Name);
        if (name is null)
            return;
        await Store(owner, EffectPreset.From(clip, name));
    }

    /// <summary>Lets the user pick a preset file and copies it into their presets; unreadable files are reported, not thrown.</summary>
    /// <param name="owner">Window the dialogs belong to.</param>
    public static async Task Import(Window owner)
    {
        var path = await Dialogs.PickOpen(owner, "Import preset", (FileTypeName, [$"*{EffectPreset.Extension}"]));
        if (path is null)
            return;
        if (UserData.TryLoadPreset(path) is not { } preset)
        {
            await Dialogs.Ask(owner, "Import preset", $"“{Path.GetFileName(path)}” isn't a preset Lampion can read.", "OK");
            return;
        }
        await Store(owner, preset);
    }

    /// <summary>Saves <paramref name="preset"/> to a file the user picks; I/O failures are shown in a dialog.</summary>
    /// <param name="owner">Window the dialogs belong to.</param>
    /// <param name="preset">The preset to write.</param>
    public static async Task Export(Window owner, EffectPreset preset)
    {
        var path = await Dialogs.PickSave(owner, "Export preset", preset.Name, ExtensionNoDot, FileTypeName);
        if (path is null)
            return;
        await Guard(owner, "Export preset", () => preset.Save(path));
    }

    /// <summary>Renames a user preset by saving it under the new name and deleting the old file.</summary>
    /// <param name="owner">Window the dialogs belong to.</param>
    /// <param name="path">The preset's current file.</param>
    /// <param name="preset">The preset stored at <paramref name="path"/>; it is not changed.</param>
    public static async Task Rename(Window owner, string path, EffectPreset preset)
    {
        var name = await AskName(owner, "Rename preset", preset.Name);
        if (name is null || name == preset.Name)
            return;
        var renamed = EffectPreset.Deserialize(EffectPreset.Serialize(preset));
        renamed.Name = name;
        if (await Store(owner, renamed, replacing: path) && !SamePath(path, UserData.PresetPath(name)))
            await Guard(owner, "Rename preset", () => UserData.DeletePreset(path));
    }

    /// <summary>Deletes a user preset after confirming; clips made from it are unaffected.</summary>
    /// <param name="owner">Window the dialogs belong to.</param>
    /// <param name="path">The preset's file.</param>
    /// <param name="preset">The preset stored at <paramref name="path"/>, for the confirmation text.</param>
    public static async Task Delete(Window owner, string path, EffectPreset preset)
    {
        if (await Dialogs.Ask(owner, "Delete preset", $"Delete the preset “{preset.Name}”? Clips already made from it stay as they are.", "Delete", "Cancel") == "Delete")
            await Guard(owner, "Delete preset", () => UserData.DeletePreset(path));
    }

    private static async Task<string?> AskName(Window owner, string title, string initial)
    {
        var name = (await Dialogs.Prompt(owner, title, "Preset name", initial))?.Trim();
        return string.IsNullOrEmpty(name) ? null : name;
    }

    /// <summary>Saves to the user's presets, asking before replacing one with the same name.</summary>
    private static async Task<bool> Store(Window owner, EffectPreset preset, string? replacing = null)
    {
        var target = UserData.PresetPath(preset.Name);
        if (File.Exists(target) && (replacing is null || !SamePath(target, replacing))
            && await Dialogs.Ask(owner, "Save preset", $"A preset called “{preset.Name}” already exists. Replace it?", "Replace", "Cancel") != "Replace")
            return false;
        return await Guard(owner, "Save preset", () => UserData.SavePreset(preset));
    }

    private static async Task<bool> Guard(Window owner, string title, Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            await Dialogs.Ask(owner, title, ex.Message, "OK");
            return false;
        }
    }

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
}
