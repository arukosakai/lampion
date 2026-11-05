using Avalonia.Controls;
using Lampion.App.Editor;
using Lampion.App.Views.ProfileEditor;
using Lampion.Core.Model.Rig;

namespace Lampion.App.Views;

/// <summary>
/// Editing one fixture profile: identity and optics on top, colour-coded channels below, and a live 3D
/// preview on the right driven by per-channel test values that are never saved.
/// </summary>
public static class ProfileEditorDialog
{
    /// <summary>Shows the profile editor modally over <paramref name="owner"/>.</summary>
    /// <param name="owner">The window the dialog belongs to.</param>
    /// <param name="state">The project to save into; the edit is committed as one undo step.</param>
    /// <param name="profile">The profile to edit. It is never modified: Save puts an edited copy with the same id in its place.</param>
    /// <param name="isNew">True for a profile not yet in the project; Save then adds it.</param>
    /// <returns>True when the user saved (the profile is then in the project).</returns>
    public static async Task<bool> Show(Window owner, EditorState state, FixtureProfile profile, bool isNew)
    {
        var window = new ProfileEditorWindow(state, profile, isNew);
        await window.ShowDialog(owner);
        return window.Saved;
    }

    /// <summary>A fresh one-channel (dimmer) profile, the starting point for ＋ New → Fixture profile.</summary>
    internal static FixtureProfile NewCustomProfile() =>
        new() { Model = "Custom fixture", Channels = { new FixtureChannel { Name = "Dimmer", Role = ChannelRole.Dimmer } } };
}
