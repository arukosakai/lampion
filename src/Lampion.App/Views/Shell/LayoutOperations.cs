using System.Text.Json;
using Avalonia.Controls;
using Lampion.App.Editor;
using Lampion.App.Views.TrussBuilder;
using Lampion.Core.Model.Stage;
using Lampion.Core.Persistence;

namespace Lampion.App.Views.Shell;

/// <summary>
/// The Stage menu's layout commands: build a truss structure, export and import a stage layout, save it as a
/// start-screen template or as the default for new shows. Failures are explained in a dialog; nothing here throws.
/// </summary>
internal sealed class LayoutOperations
{
    private readonly Window _owner;
    private readonly EditorState _state;
    private readonly StatusBar _status;

    /// <summary>Layout commands for the show in <paramref name="state"/>, with dialogs over <paramref name="owner"/> and outcomes in <paramref name="status"/>.</summary>
    public LayoutOperations(Window owner, EditorState state, StatusBar status)
    {
        _owner = owner;
        _state = state;
        _status = status;
    }

    /// <summary>Opens the truss structure builder, starting near <paramref name="near"/>; reports what was built in the status bar.</summary>
    public async Task BuildTruss(Vec3 near)
    {
        var window = new TrussBuilderWindow(_state, near);
        await window.ShowDialog(_owner);
        if (window.Result is { } summary)
            _status.Message = summary;
    }

    /// <summary>Asks for a name and saves the stage and rig as a template on the start screen.</summary>
    public async Task SaveTemplate()
    {
        var name = await Dialogs.Prompt(_owner, "Save layout as template", "Template name", _state.Project.Name);
        if (string.IsNullOrWhiteSpace(name))
            return;
        try
        {
            UserData.SaveTemplate(StageLayout.From(_state.Project), name.Trim());
            _status.Message = $"Saved template \"{name.Trim()}\" — it's on the start screen now";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await Dialogs.Ask(_owner, "Could not save template", ex.Message, "OK");
        }
    }

    /// <summary>Saves the stage and rig to a layout file the user picks.</summary>
    public async Task Export()
    {
        var path = await Dialogs.PickSave(_owner, "Export stage layout", _state.Project.Name + StageLayout.Extension, StageLayout.Extension.TrimStart('.'), "Lampion stage layout");
        if (path is null)
            return;
        try
        {
            StageLayout.From(_state.Project, Path.GetFileNameWithoutExtension(path)).Save(path);
            _status.Message = $"Exported stage layout {Path.GetFileName(path)}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await Dialogs.Ask(_owner, "Could not export", ex.Message, "OK");
        }
    }

    /// <summary>Applies a layout file to the show as one undo step, asking whether to replace the rig too when it has one.</summary>
    public async Task Import()
    {
        var path = await Dialogs.PickOpen(_owner, "Import stage layout", ("Lampion stage layout", ["*" + StageLayout.Extension]));
        if (path is null)
            return;
        StageLayout layout;
        try
        {
            layout = StageLayout.Load(path);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
        {
            await Dialogs.Ask(_owner, "Could not import", ex.Message, "OK");
            return;
        }
        var includeRig = false;
        if (layout.HasRig)
        {
            var fixtures = _state.Project.Fixtures.Count;
            var replacing = fixtures > 0 ? $" This replaces the {fixtures} fixtures and groups in this project." : "";
            var answer = await Dialogs.Ask(
                _owner,
                "Import stage layout",
                $"\"{layout.Name}\" has {layout.Stage.Objects.Count} set pieces and a rig of {layout.Fixtures.Count} fixtures in {layout.Groups.Count} groups. Import the rig too?{replacing}",
                "Set and rig",
                "Set only",
                "Cancel"
            );
            if (answer is null or "Cancel")
                return;
            includeRig = answer == "Set and rig";
        }
        layout.ApplyTo(_state.Project, includeRig);
        _state.Commit();
        _state.Deselect();
        _status.Message = $"Imported stage layout \"{layout.Name}\"{(includeRig ? " with its rig" : "")} — Ctrl+Z to undo";
    }

    /// <summary>Makes the current stage and rig what the start screen offers for new shows.</summary>
    public async Task SaveDefault()
    {
        try
        {
            UserData.SaveDefaultLayout(StageLayout.From(_state.Project));
            _status.Message = "The start screen now offers this stage and rig as your default";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await Dialogs.Ask(_owner, "Could not save default layout", ex.Message, "OK");
        }
    }

    /// <summary>Removes the default layout, so new shows start with an empty rig again.</summary>
    public async Task ForgetDefault()
    {
        try
        {
            UserData.ForgetDefaultLayout();
            _status.Message = "New projects start with an empty rig again";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await Dialogs.Ask(_owner, "Could not forget default layout", ex.Message, "OK");
        }
    }
}
