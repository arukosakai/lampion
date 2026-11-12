using Avalonia.Controls;
using Lampion.App.Editor;
using Lampion.App.Live;
using Lampion.Core.Persistence;

namespace Lampion.App.Views.Live;

/// <summary>Save console… / Load console…: moves a whole live console between projects as a <c>.lamplive</c> file.</summary>
internal static class ConsoleFiles
{
    private static readonly (string Name, string[] Patterns) FileType = ("Lampion live console", ["*" + LiveConsoleSerializer.Extension]);

    /// <summary>Asks where to save, then writes the project's console there. Failures are shown, not thrown.</summary>
    public static async Task Save(Window owner, EditorState state)
    {
        var suggested = state.Project.Name + " console" + LiveConsoleSerializer.Extension;
        if (await Dialogs.PickSave(owner, "Save live console", suggested, LiveConsoleSerializer.Extension.TrimStart('.'), FileType.Name) is not { } path)
            return;
        var project = state.Project;
        var projectPath = state.FilePath;
        try
        {
            await Task.Run(() => LiveConsoleSerializer.Save(project, projectPath, path));
        }
        catch (Exception ex)
        {
            await Dialogs.Ask(owner, "Couldn't save the console", ex.Message, "OK");
        }
    }

    /// <summary>
    /// Asks for a console file and replaces this project's console with it (one undo step), matching its fixtures and
    /// groups to this patch. Says which ones it couldn't find.
    /// </summary>
    public static async Task Load(Window owner, EditorState state)
    {
        if (await Dialogs.PickOpen(owner, "Load live console", FileType) is not { } path)
            return;
        LiveConsoleFile file;
        try
        {
            file = await Task.Run(() => LiveConsoleSerializer.Load(path));
        }
        catch (Exception ex)
        {
            await Dialogs.Ask(owner, "Couldn't load the console", $"{Path.GetFileName(path)} isn't a console file Lampion can read: {ex.Message}", "OK");
            return;
        }

        var live = state.Project.Live;
        if (live.Faders.Count + live.Pads.Count + live.Shows.Count + live.Widgets.Count > 0
            && await Dialogs.Ask(owner, "Replace this console?", "Loading replaces the set list, faders, pads and widgets here. Edit → Undo brings them back.", "Replace", "Cancel") != "Replace")
            return;

        var import = LiveConsoleSerializer.Import(file, state.Project, state.FilePath);
        LiveEdits.ReplaceConsole(state, import.Console);
        if (import.UnmatchedFixtures.Count + import.UnmatchedGroups.Count > 0)
            await Dialogs.Ask(owner, "Some targets weren't found", Describe(import), "OK");
    }

    private static string Describe(LiveConsoleImport import)
    {
        var lines = new List<string> { "The console loaded, but these aren't in this project, so faders and pads no longer drive them:" };
        if (import.UnmatchedFixtures.Count > 0)
            lines.Add("Fixtures (no fixture with the same universe and address): " + string.Join(", ", import.UnmatchedFixtures));
        if (import.UnmatchedGroups.Count > 0)
            lines.Add("Groups (no group with the same name): " + string.Join(", ", import.UnmatchedGroups));
        lines.Add("Right-click a fader or pad → Edit… to pick their fixtures again.");
        return string.Join(Environment.NewLine + Environment.NewLine, lines);
    }
}
