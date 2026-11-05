using System.Text.Json;
using Avalonia.Controls;
using Lampion.App.Editor;
using Lampion.Core.Model;
using Lampion.Core.Persistence;

namespace Lampion.App.Views;

/// <summary>The dialogs around user presets, shared by the Effects panel, the inspector and the timeline.</summary>
internal static class PresetActions
{
    private const string FileTypeName = "Lampion effect preset";
    private static readonly string ExtensionNoDot = EffectPreset.Extension.TrimStart('.');

    /// <summary>Built-in presets first (no path: they can't be renamed or deleted), then the user's.</summary>
    public static List<(string? Path, EffectPreset Preset)> Library() =>
        [.. BuiltInPresets.All().Select(p => ((string?)null, p)), .. UserData.Presets().Select(p => ((string?)p.Path, p.Preset))];

    public static async Task SaveFromClip(Window owner, LightClip clip)
    {
        var name = await AskName(owner, "Save as preset", clip.Name);
        if (name is null)
            return;
        await Store(owner, EffectPreset.From(clip, name));
    }

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

    public static async Task Export(Window owner, EffectPreset preset)
    {
        var path = await Dialogs.PickSave(owner, "Export preset", preset.Name, ExtensionNoDot, FileTypeName);
        if (path is null)
            return;
        await Guard(owner, "Export preset", () => preset.Save(path));
    }

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
