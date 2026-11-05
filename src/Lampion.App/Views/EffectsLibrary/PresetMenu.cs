using Avalonia.Controls;
using Lampion.App.Editor;
using Lampion.Core.Model.Timeline;
using Lampion.Core.Persistence;

namespace Lampion.App.Views.EffectsLibrary;

/// <summary>The right-click menu on a preset: add or apply it, export it, and rename or delete the user's own.</summary>
internal static class PresetMenu
{
    /// <summary>Builds the menu for one preset.</summary>
    /// <param name="state">The editor state, for applying to the selected clip.</param>
    /// <param name="path">The preset's file; null for built-in presets, which can't be renamed or deleted.</param>
    /// <param name="preset">The preset.</param>
    /// <param name="addAtPlayhead">Adds the preset as a new clip at the playhead.</param>
    /// <param name="withOwner">Runs a dialog-showing action with the owning window, if there is one.</param>
    public static ContextMenu Create(EditorState state, string? path, EffectPreset preset, Action addAtPlayhead, Action<Func<Window, Task>> withOwner)
    {
        var menu = new ContextMenu();
        MenuItem Add(string header, Action action)
        {
            var item = new MenuItem { Header = header };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
            return item;
        }
        Add("Add at playhead", addAtPlayhead);
        var apply = Add("Apply to selected clip", () =>
        {
            if (state.PrimaryClip is LightClip clip)
                TimelineOps.ApplyPreset(state, clip, preset);
        });
        menu.Opening += (_, _) => apply.IsEnabled = state.PrimaryClip is LightClip;
        menu.Items.Add(new Separator());
        Add("Export…", () => withOwner(owner => PresetActions.Export(owner, preset)));
        if (path is not null)
        {
            Add("Rename…", () => withOwner(owner => PresetActions.Rename(owner, path, preset)));
            Add("Delete…", () => withOwner(owner => PresetActions.Delete(owner, path, preset)));
        }
        return menu;
    }
}
