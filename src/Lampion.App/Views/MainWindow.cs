using Avalonia;
using Avalonia.Controls;
using Lampion.App.Controls;
using Lampion.App.Editor;
using Lampion.App.Media;
using Lampion.App.Output;
using Lampion.App.Views.Shell;
using Lampion.Core.Model;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Views;

/// <summary>
/// The After Effects-style workspace: Project and Effects &amp; Presets on the left, the program monitor
/// (Stage 3D / Video / DMX output) in the middle, Effect Controls on the right and the timeline across
/// the bottom. It only composes the panels and shell components; they do the work.
/// </summary>
internal sealed class MainWindow : Window
{
    private readonly MediaEngine _media = new();
    private readonly DmxOutput _output;
    private readonly FileOperations _files;
    private readonly FrameLoop _loop;

    /// <summary>A workspace editing <paramref name="project"/>, saved at <paramref name="path"/> (null while unsaved).</summary>
    public MainWindow(Project project, string? path)
    {
        Width = 1680;
        Height = 1000;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var state = new EditorState(project, path);
        var stage = new StagePanel(state);
        stage.View.Video = _media.Video;
        var timeline = new TimelinePanel(state);
        var zoomToFit = timeline.Timeline.ZoomToFit;
        var video = new VideoMonitor(_media.Video) { Message = _media.Error };
        var status = new StatusBar { Message = _media.Error };

        _output = new DmxOutput(status);
        _files = new FileOperations(this, state, _media, status, zoomToFit);
        var import = new MediaImport(this, state, _media, status, zoomToFit);
        var models = new ModelImport(this, state, _files, status);
        var layouts = new LayoutOperations(this, state, status);
        var commands = new EditorCommands(this, state, _files, import, models, layouts, stage, timeline.Timeline, _output);
        stage.OpenTrussBuilder = commands.BuildTruss.Run;
        stage.ImportModel = commands.ImportModel.Run;

        var projectPanel = new ProjectPanel(state, timeline.Timeline, () => import.Import(TrackKind.Audio), () => import.Import(TrackKind.Video));
        var effects = new EffectsPanel(state, timeline.Timeline);
        var inspector = new InspectorPanel(state) { OpenProjectSettings = commands.ProjectSettings.Run };
        var monitor = ProgramMonitor.Build(stage, video, new DmxOutputView(state));
        Content = Workspace(MainMenu.Build(commands), projectPanel, effects, monitor, inspector, timeline, status.View);

        new ShortcutMap(commands).Attach(this);
        FileDropImport.Attach(this, import, models, status);
        _ = new WaveformLoader(state, timeline.Timeline.InvalidateVisual);
        var title = new WindowTitle(this, state);
        title.Refresh();
        _loop = new FrameLoop(state, _media, stage.View, video, _output, status, title);
        _loop.Start();
        Opened += (_, _) => zoomToFit();
    }

    // Menu on top; Project | Program | Effect Controls; a splitter; the timeline; the status line.
    private static Grid Workspace(Menu menu, Control projectPanel, Control effects, Control monitor, Control inspector, Control timeline, Control status)
    {
        var center = new Grid { ColumnDefinitions = new ColumnDefinitions("310,4,*,4,370") };
        center.Children.Add(PanelLayout.TabbedPanel(0, ("Project", projectPanel), ("Effects", effects)));
        center.Children.Add(PanelLayout.Splitter(1, vertical: true));
        center.Children.Add(PanelLayout.Panel("Program", monitor, 2));
        center.Children.Add(PanelLayout.Splitter(3, vertical: true));
        center.Children.Add(PanelLayout.Panel("Effect Controls", inspector, 4));

        var main = new Grid { RowDefinitions = new RowDefinitions("Auto,*,4,340,Auto"), Margin = new Thickness(4, 0, 4, 2) };
        main.Children.Add(menu);
        Grid.SetRow(center, 1);
        main.Children.Add(center);
        var rowSplitter = PanelLayout.Splitter(0, vertical: false);
        Grid.SetRow(rowSplitter, 2);
        main.Children.Add(rowSplitter);
        var timelinePanel = PanelLayout.Panel("Timeline", timeline, 0);
        Grid.SetRow(timelinePanel, 3);
        main.Children.Add(timelinePanel);
        Grid.SetRow(status, 4);
        main.Children.Add(status);
        return main;
    }

    /// <inheritdoc />
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (!_files.ConfirmClose(e))
            return;
        _loop.Stop();
        _media.Dispose();
        _output.Dispose();
    }
}
