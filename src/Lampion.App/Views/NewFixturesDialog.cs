using Avalonia.Controls;
using Lampion.App.Editor;
using Lampion.App.Views.NewFixtures;

namespace Lampion.App.Views;

/// <summary>
/// Patching new fixtures, styled after Premiere's New Sequence dialog: presets on the left, what the
/// selected one is on the right, and the few choices that matter below.
/// </summary>
public static class NewFixturesDialog
{
    /// <summary>Shows the dialog modally over <paramref name="owner"/> and patches on OK, as one undo step.</summary>
    /// <param name="owner">The window the dialog belongs to.</param>
    /// <param name="state">The project to patch into.</param>
    /// <returns>Ids of the fixtures added, in patch order (possibly empty when nothing fit); null when cancelled.</returns>
    public static async Task<List<string>?> Show(Window owner, EditorState state)
    {
        var window = new NewFixturesWindow(state);
        await window.ShowDialog(owner);
        return window.Added;
    }
}
