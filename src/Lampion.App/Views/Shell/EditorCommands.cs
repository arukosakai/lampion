using Avalonia.Controls;
using Lampion.App.Controls;
using Lampion.App.Editor;
using Lampion.App.Output;
using Lampion.App.Views.Output;
using Lampion.App.Views.Live;
using Lampion.App.Rendering;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Views.Shell;

/// <summary>
/// Every command of the workspace, defined once so the menu bar and the shortcut map run the same thing.
/// Gestures shown here are what the menu displays; <see cref="ShortcutMap"/> binds the keys.
/// </summary>
internal sealed class EditorCommands
{
    // Arrow keys step one frame at 30 fps, or a second with Shift.
    private const double FrameStep = 1 / 30.0;
    private const double ZoomStep = 1.4;

    private LiveWindow? _live;

    /// <summary>Builds the commands over the workspace's parts.</summary>
    /// <param name="window">Owner of dialogs, closed by <see cref="Exit"/>.</param>
    /// <param name="state">The editor state.</param>
    /// <param name="files">Open, save and export.</param>
    /// <param name="import">Media import.</param>
    /// <param name="models">3D model import.</param>
    /// <param name="layouts">Stage layout import, export and templates.</param>
    /// <param name="stage">The Stage 3D monitor.</param>
    /// <param name="timeline">The timeline control.</param>
    /// <param name="output">The DMX output.</param>
    public EditorCommands(Window window, EditorState state, FileOperations files, MediaImport import, ModelImport models, LayoutOperations layouts, StagePanel stage, TimelineControl timeline, DmxOutput output)
    {
        // Zoom keeps the time just right of the track headers in place, where the eye usually is.
        var zoomAnchor = TimelineControl.HeaderWidth + 200;

        NewProject = new("New / start screen…", () => _ = files.NewProject(), "Ctrl+N");
        Open = new("Open…", () => _ = files.OpenProject(), "Ctrl+O");
        Save = new("Save", () => _ = files.Save(saveAs: false), "Ctrl+S");
        SaveAs = new("Save as…", () => _ = files.Save(saveAs: true), "Ctrl+Shift+S");
        ProjectSettings = new("Project settings…", () => _ = Dialogs.ProjectSettings(window, state), "Ctrl+Shift+P");
        ImportAudio = new("Import audio…", () => _ = import.Import(TrackKind.Audio), "Ctrl+I");
        ImportVideo = new("Import video…", () => _ = import.Import(TrackKind.Video));
        ExportQlc = new("Export to QLC+…", () => _ = files.ExportQlc(), "Ctrl+E");
        Exit = new("Exit", window.Close);

        Undo = new("Undo", state.Undo, "Ctrl+Z");
        Redo = new("Redo", state.Redo, "Ctrl+Y");
        Copy = new("Copy clip", () => TimelineOps.Copy(state), "Ctrl+C");
        Paste = new("Paste clip at playhead", () => TimelineOps.Paste(state), "Ctrl+V");
        Duplicate = new("Duplicate", () => TimelineOps.Duplicate(state), "Ctrl+D");
        Delete = new("Delete", () => TimelineOps.DeleteSelection(state), "Delete");
        Deselect = new("Deselect all", state.Deselect, "Escape");

        PlayPause = new("Play / pause", state.TogglePlay, "Space");
        GoToStart = new("Go to start", () => state.Seek(0), "Home");
        GoToEnd = new("Go to end", () => state.Seek(state.Project.ContentEnd), "End");
        StepBack = new("Back one frame", () => state.Seek(state.Playhead - FrameStep), "Left");
        StepForward = new("Forward one frame", () => state.Seek(state.Playhead + FrameStep), "Right");
        StepBackSecond = new("Back one second", () => state.Seek(state.Playhead - 1), "Shift+Left");
        StepForwardSecond = new("Forward one second", () => state.Seek(state.Playhead + 1), "Shift+Right");
        SplitAtPlayhead = new("Split at playhead", () => TimelineOps.SplitAtPlayhead(state), "Ctrl+K");
        AddMarker = new("Add marker", () => TimelineOps.AddMarker(state), "M");
        NewLightTrack = new("New light track", () => TimelineOps.AddLightTrack(state, state.SelectedFixtureIds));
        ZoomIn = new("Zoom in", () => timeline.ZoomAround(ZoomStep, zoomAnchor), "OemPlus");
        ZoomOut = new("Zoom out", () => timeline.ZoomAround(1 / ZoomStep, zoomAnchor), "OemMinus");
        ZoomToFit = new("Zoom to fit", timeline.ZoomToFit, "OemPipe");

        PerspectiveView = new("Perspective view", () => stage.SetPreset(ViewPreset.Free), "D1");
        TopView = new("Top view", () => stage.SetPreset(ViewPreset.Top), "D2");
        FrontView = new("Front view", () => stage.SetPreset(ViewPreset.Front), "D3");
        RightView = new("Right view", () => stage.SetPreset(ViewPreset.Right), "D4");
        ToggleProjection = new("Toggle perspective / ortho", () => stage.View.Camera.ToggleProjection(), "D5");
        HandTool = new("Hand tool", () => stage.SetTool(BuilderTool.Hand), "Q");
        MoveTool = new("Move tool", () => stage.SetTool(BuilderTool.Move), "W");
        AimTool = new("Aim tool", () => stage.SetTool(BuilderTool.Aim), "A");
        FrameSelection = new("Frame selection", stage.View.FrameSelection, "F");
        BuildTruss = new("Truss structure…", () => _ = layouts.BuildTruss(Controls.Stage3D.StageGeometry.ToVec(stage.View.Camera.Target)));
        ImportModel = new("Import 3D model…", () => _ = models.Import());
        ExportLayout = new("Export stage layout…", () => _ = layouts.Export());
        ImportLayout = new("Import stage layout…", () => _ = layouts.Import());
        SaveTemplate = new("Save layout as template…", () => _ = layouts.SaveTemplate());
        UseAsDefaultLayout = new("Use this layout as the default", () => _ = layouts.SaveDefault());
        ForgetDefaultLayout = new("Forget default layout", () => _ = layouts.ForgetDefault());

        ToggleOutput = new("Send DMX output", output.Toggle) { IsChecked = () => output.IsSending };
        OutputSetup = new("Output setup…", () => _ = OutputSetupWindow.Show(window, output));
        LiveConsole = new("Live console…", () => LiveWindow.ShowFor(window, state, output, ref _live), "Ctrl+L");
    }

    /// <summary>File → New / start screen.</summary>
    public ShellCommand NewProject { get; }

    /// <summary>File → Open.</summary>
    public ShellCommand Open { get; }

    /// <summary>File → Save.</summary>
    public ShellCommand Save { get; }

    /// <summary>File → Save as.</summary>
    public ShellCommand SaveAs { get; }

    /// <summary>File → Project settings.</summary>
    public ShellCommand ProjectSettings { get; }

    /// <summary>File → Import audio.</summary>
    public ShellCommand ImportAudio { get; }

    /// <summary>File → Import video.</summary>
    public ShellCommand ImportVideo { get; }

    /// <summary>File → Export to QLC+.</summary>
    public ShellCommand ExportQlc { get; }

    /// <summary>File → Exit (asks about unsaved changes first).</summary>
    public ShellCommand Exit { get; }

    /// <summary>Edit → Undo.</summary>
    public ShellCommand Undo { get; }

    /// <summary>Edit → Redo.</summary>
    public ShellCommand Redo { get; }

    /// <summary>Edit → Copy clip.</summary>
    public ShellCommand Copy { get; }

    /// <summary>Edit → Paste clip at playhead.</summary>
    public ShellCommand Paste { get; }

    /// <summary>Edit → Duplicate.</summary>
    public ShellCommand Duplicate { get; }

    /// <summary>Edit → Delete (the picked keyframe, else the selection).</summary>
    public ShellCommand Delete { get; }

    /// <summary>Clears the selection.</summary>
    public ShellCommand Deselect { get; }

    /// <summary>Timeline → Play / pause.</summary>
    public ShellCommand PlayPause { get; }

    /// <summary>Timeline → Go to start.</summary>
    public ShellCommand GoToStart { get; }

    /// <summary>Moves the playhead to where the content ends.</summary>
    public ShellCommand GoToEnd { get; }

    /// <summary>Moves the playhead back one frame (1/30 s).</summary>
    public ShellCommand StepBack { get; }

    /// <summary>Moves the playhead forward one frame (1/30 s).</summary>
    public ShellCommand StepForward { get; }

    /// <summary>Moves the playhead back one second.</summary>
    public ShellCommand StepBackSecond { get; }

    /// <summary>Moves the playhead forward one second.</summary>
    public ShellCommand StepForwardSecond { get; }

    /// <summary>Timeline → Split at playhead.</summary>
    public ShellCommand SplitAtPlayhead { get; }

    /// <summary>Timeline → Add marker.</summary>
    public ShellCommand AddMarker { get; }

    /// <summary>Timeline → New light track (for the selected fixtures).</summary>
    public ShellCommand NewLightTrack { get; }

    /// <summary>Zooms the timeline in.</summary>
    public ShellCommand ZoomIn { get; }

    /// <summary>Zooms the timeline out.</summary>
    public ShellCommand ZoomOut { get; }

    /// <summary>Timeline → Zoom to fit.</summary>
    public ShellCommand ZoomToFit { get; }

    /// <summary>Stage → Perspective view.</summary>
    public ShellCommand PerspectiveView { get; }

    /// <summary>Stage → Top view.</summary>
    public ShellCommand TopView { get; }

    /// <summary>Stage → Front view.</summary>
    public ShellCommand FrontView { get; }

    /// <summary>Stage → Right view.</summary>
    public ShellCommand RightView { get; }

    /// <summary>Stage → Toggle perspective / ortho.</summary>
    public ShellCommand ToggleProjection { get; }

    /// <summary>Picks the stage hand (pan) tool.</summary>
    public ShellCommand HandTool { get; }

    /// <summary>Stage → Move tool.</summary>
    public ShellCommand MoveTool { get; }

    /// <summary>Stage → Aim tool.</summary>
    public ShellCommand AimTool { get; }

    /// <summary>Stage → Frame selection.</summary>
    public ShellCommand FrameSelection { get; }

    /// <summary>Stage → Export stage layout.</summary>
    public ShellCommand ExportLayout { get; }

    /// <summary>Stage → Import stage layout.</summary>
    public ShellCommand ImportLayout { get; }

    /// <summary>Stage → Save layout as template.</summary>
    public ShellCommand SaveTemplate { get; }

    /// <summary>Stage → Use this layout as the default.</summary>
    public ShellCommand UseAsDefaultLayout { get; }

    /// <summary>Stage → Forget default layout.</summary>
    public ShellCommand ForgetDefaultLayout { get; }

    /// <summary>Stage → Truss structure: build an arch or cube with lights.</summary>
    public ShellCommand BuildTruss { get; }

    /// <summary>Stage → Import 3D model: a glTF or OBJ file onto the stage.</summary>
    public ShellCommand ImportModel { get; }

    /// <summary>Output → Send DMX output (a check box), to the device chosen in Output Setup.</summary>
    public ShellCommand ToggleOutput { get; }

    /// <summary>Output → Output Setup: Art-Net or a USB DMX interface.</summary>
    public ShellCommand OutputSetup { get; }

    /// <summary>Output → Live console: set list, faders and effect pads driving the rig in real time.</summary>
    public ShellCommand LiveConsole { get; }
}
