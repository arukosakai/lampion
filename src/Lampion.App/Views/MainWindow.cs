using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lampion.App.Controls;
using Lampion.App.Editor;
using Lampion.App.Media;
using Lampion.App.Rendering;
using Lampion.Core.Media;
using Lampion.Core.Model;
using Lampion.Core.Output;
using Lampion.Core.Persistence;

namespace Lampion.App.Views;

/// <summary>
/// Premiere-style workspace: project bin on the left, program monitor (3D stage / video / DMX) in the middle,
/// effect controls on the right and the timeline across the bottom.
/// </summary>
public sealed class MainWindow : Window
{
    private readonly EditorState _state;
    private readonly StagePanel _stage;
    private readonly TimelinePanel _timeline;
    private readonly MediaEngine _media = new();
    private readonly VideoMonitor _videoMonitor;
    private readonly TextBlock _status = new() { Foreground = Brushes.Gray, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
    private readonly Stopwatch _artNetClock = Stopwatch.StartNew();
    private ArtNetSender? _artNet;
    private string? _message;
    private bool _closingConfirmed;

    public MainWindow(string? openPath = null)
    {
        Width = 1680;
        Height = 1000;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var project = Project.CreateDemo();
        string? path = null;
        if (openPath is not null && File.Exists(openPath))
        {
            project = ProjectSerializer.Load(openPath);
            path = openPath;
        }
        _state = new EditorState(project, path);
        _stage = new StagePanel(_state);
        _stage.View.Video = _media.Video;
        _timeline = new TimelinePanel(_state);
        _videoMonitor = new VideoMonitor(_media.Video) { Message = _media.Error };
        var project_ = new ProjectPanel(_state, _timeline.Timeline, () => ImportMedia(TrackKind.Audio), () => ImportMedia(TrackKind.Video));
        var inspector = new InspectorPanel(_state);
        var dmx = new DmxMonitor(_state);

        var universePicker = Ui.Number(1, v =>
        {
            dmx.Universe = (int)v;
            dmx.InvalidateVisual();
        }, 1, 64, 1, "0");
        universePicker.Width = 100;
        var dmxPanel = new DockPanel();
        var dmxBar = Ui.Row(Ui.Label("Universe"), universePicker);
        dmxBar.Margin = new Thickness(6);
        DockPanel.SetDock(dmxBar, Dock.Top);
        dmxPanel.Children.Add(dmxBar);
        dmxPanel.Children.Add(new ScrollViewer { Content = dmx, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto });

        var monitors = new TabControl
        {
            Items =
            {
                new TabItem { Header = "Stage 3D", Content = _stage },
                new TabItem { Header = "Video", Content = _videoMonitor },
                new TabItem { Header = "DMX output", Content = dmxPanel },
            },
        };

        var center = new Grid { ColumnDefinitions = new ColumnDefinitions("310,4,*,4,370") };
        center.Children.Add(Panel("Project", project_, 0));
        center.Children.Add(Splitter(1, vertical: true));
        center.Children.Add(Panel("Program", monitors, 2));
        center.Children.Add(Splitter(3, vertical: true));
        center.Children.Add(Panel("Effect Controls", inspector, 4));

        var main = new Grid { RowDefinitions = new RowDefinitions("Auto,*,4,340,Auto"), Margin = new Thickness(4, 0, 4, 2) };
        var menu = BuildMenu();
        main.Children.Add(menu);
        Grid.SetRow(center, 1);
        main.Children.Add(center);
        var rowSplitter = Splitter(0, vertical: false);
        Grid.SetRow(rowSplitter, 2);
        main.Children.Add(rowSplitter);
        var timelinePanel = Panel("Timeline", _timeline, 0);
        Grid.SetRow(timelinePanel, 3);
        main.Children.Add(timelinePanel);
        Grid.SetRow(_status, 4);
        _status.Margin = new Thickness(6, 3);
        main.Children.Add(_status);
        Content = main;

        AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
        _state.Changed += UpdateTitle;
        _state.ProjectReplaced += LoadWaveforms;
        UpdateTitle();
        LoadWaveforms();
        if (_media.Error is not null)
            _message = _media.Error;

        // One clock drives everything: advance the playhead, slave the media, rebuild the 3D scene, send DMX.
        var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(15), DispatcherPriority.Render, (_, _) => Tick());
        timer.Start();
        Opened += (_, _) => _timeline.Timeline.ZoomToFit();
    }

    private void Tick()
    {
        _state.Tick();
        _media.Sync(_state);
        _stage.View.UpdateScene();
        _videoMonitor.Update();
        if (_artNet is not null && _artNetClock.ElapsedMilliseconds >= 25)
        {
            _artNetClock.Restart();
            try
            {
                _artNet.Send(_state.CurrentFrame);
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                _message = "Art-Net: " + ex.Message;
            }
        }
        _status.Text = StatusText();
    }

    private string StatusText()
    {
        var file = _state.FilePath is null ? "unsaved project" : _state.FilePath;
        var art = _artNet is null ? "Art-Net off" : "Art-Net → broadcast";
        return $"{file}{(_state.Dirty ? " •" : "")}   |   {_state.Project.Fixtures.Count} fixtures   |   {art}{(_message is null ? "" : "   |   " + _message)}";
    }

    private void UpdateTitle() => Title = $"Lampion — {_state.Project.Name}{(_state.Dirty ? " •" : "")}";

    private static Border Panel(string title, Control content, int column)
    {
        var header = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x2B, 0x2B, 0x2B)),
            Child = new TextBlock { Text = title, Classes = { "header" } },
        };
        var dock = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        dock.Children.Add(header);
        dock.Children.Add(content);
        var border = new Border { Child = dock, Classes = { "panel" }, Margin = new Thickness(0, 2) };
        Grid.SetColumn(border, column);
        return border;
    }

    private static GridSplitter Splitter(int index, bool vertical)
    {
        var splitter = new GridSplitter
        {
            Background = Brushes.Transparent,
            ResizeDirection = vertical ? GridResizeDirection.Columns : GridResizeDirection.Rows,
        };
        if (vertical)
            Grid.SetColumn(splitter, index);
        return splitter;
    }

    // ---- Menu ------------------------------------------------------------------------------------------

    private Menu BuildMenu()
    {
        MenuItem Item(string header, Action action, string? gesture = null)
        {
            var item = new MenuItem { Header = header };
            if (gesture is not null)
                item.InputGesture = KeyGesture.Parse(gesture);
            item.Click += (_, _) => action();
            return item;
        }

        var artNet = new MenuItem { Header = "Send Art-Net (broadcast)", ToggleType = MenuItemToggleType.CheckBox };
        artNet.Click += (_, _) =>
        {
            ToggleArtNet();
            artNet.IsChecked = _artNet is not null;
        };

        return new Menu
        {
            Items =
            {
                new MenuItem
                {
                    Header = "_File",
                    Items =
                    {
                        Item("New", () => _ = NewProject(), "Ctrl+N"),
                        Item("Open…", () => _ = OpenProject(), "Ctrl+O"),
                        Item("Save", () => _ = Save(false), "Ctrl+S"),
                        Item("Save as…", () => _ = Save(true), "Ctrl+Shift+S"),
                        new Separator(),
                        Item("Import audio…", () => _ = ImportMedia(TrackKind.Audio), "Ctrl+I"),
                        Item("Import video…", () => _ = ImportMedia(TrackKind.Video)),
                        new Separator(),
                        Item("Export to QLC+…", () => _ = Dialogs.ExportQlc(this, _state.Project, _state.FilePath), "Ctrl+E"),
                        new Separator(),
                        Item("Exit", Close),
                    },
                },
                new MenuItem
                {
                    Header = "_Edit",
                    Items =
                    {
                        Item("Undo", _state.Undo, "Ctrl+Z"),
                        Item("Redo", _state.Redo, "Ctrl+Y"),
                        new Separator(),
                        Item("Copy clip", () => TimelineOps.Copy(_state), "Ctrl+C"),
                        Item("Paste clip at playhead", () => TimelineOps.Paste(_state), "Ctrl+V"),
                        Item("Duplicate", () => TimelineOps.Duplicate(_state), "Ctrl+D"),
                        Item("Delete", () => TimelineOps.DeleteSelection(_state), "Delete"),
                    },
                },
                new MenuItem
                {
                    Header = "_Timeline",
                    Items =
                    {
                        Item("Play / pause", _state.TogglePlay, "Space"),
                        Item("Go to start", () => _state.Seek(0), "Home"),
                        Item("Split at playhead", () => TimelineOps.SplitAtPlayhead(_state), "Ctrl+K"),
                        Item("Add marker", () => TimelineOps.AddMarker(_state), "M"),
                        Item("New light track", () => TimelineOps.AddLightTrack(_state, _state.SelectedFixtureIds)),
                        Item("Zoom to fit", _timeline.Timeline.ZoomToFit, "OemPipe"),
                    },
                },
                new MenuItem
                {
                    Header = "_Stage",
                    Items =
                    {
                        Item("Perspective view", () => _stage.SetPreset(ViewPreset.Perspective), "D1"),
                        Item("Top view", () => _stage.SetPreset(ViewPreset.Top), "D2"),
                        Item("Front view", () => _stage.SetPreset(ViewPreset.Front), "D3"),
                        Item("Side view", () => _stage.SetPreset(ViewPreset.Side), "D4"),
                        new Separator(),
                        Item("Move tool", () => _stage.SetTool(BuilderTool.Move), "W"),
                        Item("Aim tool", () => _stage.SetTool(BuilderTool.Aim), "A"),
                        Item("Frame selection", _stage.View.FrameSelection, "F"),
                        new Separator(),
                        Item("Export stage layout…", () => _ = ExportLayout()),
                        Item("Import stage layout…", () => _ = ImportLayout()),
                        Item("Use this layout for new projects", () => _ = SaveDefaultLayout()),
                        Item("Forget default layout", ForgetDefaultLayout),
                    },
                },
                new MenuItem { Header = "_Output", Items = { artNet } },
            },
        };
    }

    private void ToggleArtNet()
    {
        if (_artNet is not null)
        {
            _artNet.Dispose();
            _artNet = null;
            return;
        }
        try
        {
            _artNet = new ArtNetSender();
            _message = null;
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            _message = "Art-Net: " + ex.Message;
        }
    }

    // ---- Keyboard --------------------------------------------------------------------------------------

    private void OnKey(object? sender, KeyEventArgs e)
    {
        // Let text fields keep their keys; only chorded shortcuts pass through them.
        var typing = FocusManager?.GetFocusedElement() is TextBox;
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        if (ctrl)
        {
            Action? action = e.Key switch
            {
                Key.Z when shift => _state.Redo,
                Key.Z => _state.Undo,
                Key.Y => _state.Redo,
                Key.S => () => _ = Save(shift),
                Key.O => () => _ = OpenProject(),
                Key.N => () => _ = NewProject(),
                Key.E => () => _ = Dialogs.ExportQlc(this, _state.Project, _state.FilePath),
                Key.I => () => _ = ImportMedia(TrackKind.Audio),
                Key.K => () => TimelineOps.SplitAtPlayhead(_state),
                Key.D => () => TimelineOps.Duplicate(_state),
                Key.C when !typing => () => TimelineOps.Copy(_state),
                Key.V when !typing => () => TimelineOps.Paste(_state),
                _ => null,
            };
            if (action is not null)
            {
                action();
                e.Handled = true;
            }
            return;
        }
        if (typing)
            return;
        Action? plain = e.Key switch
        {
            Key.Space => _state.TogglePlay,
            Key.Home => () => _state.Seek(0),
            Key.End => () => _state.Seek(_state.Project.ContentEnd),
            Key.Delete or Key.Back => () => TimelineOps.DeleteSelection(_state),
            Key.M => () => TimelineOps.AddMarker(_state),
            Key.OemPlus or Key.Add => () => _timeline.Timeline.ZoomAround(1.4, TimelineControl.HeaderWidth + 200),
            Key.OemMinus or Key.Subtract => () => _timeline.Timeline.ZoomAround(1 / 1.4, TimelineControl.HeaderWidth + 200),
            Key.OemPipe or Key.OemBackslash => _timeline.Timeline.ZoomToFit,
            Key.Left => () => _state.Seek(_state.Playhead - (shift ? 1 : 1 / 30.0)),
            Key.Right => () => _state.Seek(_state.Playhead + (shift ? 1 : 1 / 30.0)),
            Key.D1 => () => _stage.SetPreset(ViewPreset.Perspective),
            Key.D2 => () => _stage.SetPreset(ViewPreset.Top),
            Key.D3 => () => _stage.SetPreset(ViewPreset.Front),
            Key.D4 => () => _stage.SetPreset(ViewPreset.Side),
            Key.W => () => _stage.SetTool(BuilderTool.Move),
            Key.A => () => _stage.SetTool(BuilderTool.Aim),
            Key.F => _stage.View.FrameSelection,
            Key.Escape => _state.Deselect,
            _ => null,
        };
        if (plain is null)
            return;
        plain();
        e.Handled = true;
    }

    // ---- Files -----------------------------------------------------------------------------------------

    /// <summary>Asks to save unsaved changes; false means the user cancelled.</summary>
    private async Task<bool> ConfirmDiscard()
    {
        if (!_state.Dirty)
            return true;
        var answer = await Dialogs.Ask(this, "Unsaved changes", $"Save changes to \"{_state.Project.Name}\" first?", "Save", "Don't save", "Cancel");
        return answer switch
        {
            "Save" => await Save(false),
            "Don't save" => true,
            _ => false,
        };
    }

    private async Task NewProject()
    {
        if (!await ConfirmDiscard())
            return;
        var project = new Project();
        project.Profiles.AddRange(BuiltInProfiles.All().Select(p => p.Clone(newId: false)));
        var lightTracks = new List<Track>();
        if (LoadDefaultLayout() is { } layout)
        {
            layout.ApplyTo(project, includeRig: true);
            // A ready rig deserves ready tracks: one per group, so the first clip can go straight on.
            lightTracks.AddRange(project.Groups.Select(g => new Track { Name = g.Name, GroupIds = [g.Id], Order = FixtureOrder.LeftToRight }));
            _message = $"New project from your default stage layout \"{layout.Name}\"";
        }
        if (lightTracks.Count == 0)
            lightTracks.Add(new Track { Name = "Lights 1" });
        project.Tracks.AddRange(lightTracks);
        project.Tracks.AddRange([new Track { Name = "Audio", Kind = TrackKind.Audio }, new Track { Name = "Video", Kind = TrackKind.Video }]);
        _media.StopAll();
        _state.Load(project, null);
    }

    // ---- Stage layouts ---------------------------------------------------------------------------------

    private static string DefaultLayoutPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lampion", "default" + StageLayout.Extension);

    private StageLayout? LoadDefaultLayout()
    {
        if (!File.Exists(DefaultLayoutPath))
            return null;
        try
        {
            return StageLayout.Load(DefaultLayoutPath);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException)
        {
            _message = "Default stage layout could not be read: " + ex.Message;
            return null;
        }
    }

    private async Task ExportLayout()
    {
        var path = await Dialogs.PickSave(this, "Export stage layout", _state.Project.Name + StageLayout.Extension, StageLayout.Extension.TrimStart('.'), "Lampion stage layout");
        if (path is null)
            return;
        try
        {
            StageLayout.From(_state.Project, Path.GetFileNameWithoutExtension(path)).Save(path);
            _message = $"Exported stage layout {Path.GetFileName(path)}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await Dialogs.Ask(this, "Could not export", ex.Message, "OK");
        }
    }

    private async Task ImportLayout()
    {
        var path = await Dialogs.PickOpen(this, "Import stage layout", ("Lampion stage layout", ["*" + StageLayout.Extension]));
        if (path is null)
            return;
        StageLayout layout;
        try
        {
            layout = StageLayout.Load(path);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            await Dialogs.Ask(this, "Could not import", ex.Message, "OK");
            return;
        }
        var includeRig = false;
        if (layout.HasRig)
        {
            var replacing = _state.Project.Fixtures.Count > 0 ? $" This replaces the {_state.Project.Fixtures.Count} fixtures and groups in this project." : "";
            var answer = await Dialogs.Ask(
                this,
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
        _message = $"Imported stage layout \"{layout.Name}\"{(includeRig ? " with its rig" : "")} — Ctrl+Z to undo";
    }

    private async Task SaveDefaultLayout()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DefaultLayoutPath)!);
            StageLayout.From(_state.Project).Save(DefaultLayoutPath);
            _message = "File → New will now start from this stage and rig";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await Dialogs.Ask(this, "Could not save default layout", ex.Message, "OK");
        }
    }

    private void ForgetDefaultLayout()
    {
        if (File.Exists(DefaultLayoutPath))
            File.Delete(DefaultLayoutPath);
        _message = "New projects start with an empty rig again";
    }

    private async Task OpenProject()
    {
        if (!await ConfirmDiscard())
            return;
        var path = await Dialogs.PickOpen(this, "Open show", ("Lampion show", ["*" + ProjectSerializer.Extension]));
        if (path is null)
            return;
        try
        {
            _media.StopAll();
            _state.Load(ProjectSerializer.Load(path), path);
            _timeline.Timeline.ZoomToFit();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            await Dialogs.Ask(this, "Could not open", ex.Message, "OK");
        }
    }

    private async Task<bool> Save(bool saveAs)
    {
        var path = _state.FilePath;
        if (saveAs || path is null)
            path = await Dialogs.PickSave(this, "Save show", _state.Project.Name + ProjectSerializer.Extension, ProjectSerializer.Extension.TrimStart('.'), "Lampion show");
        if (path is null)
            return false;
        try
        {
            ProjectSerializer.Save(_state.Project, path);
            _state.MarkSaved(path);
            _message = $"Saved {Path.GetFileName(path)}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await Dialogs.Ask(this, "Could not save", ex.Message, "OK");
            return false;
        }
    }

    private async Task ImportMedia(TrackKind kind)
    {
        var patterns = kind == TrackKind.Audio
            ? new[] { "*.mp3", "*.wav", "*.ogg", "*.flac", "*.m4a", "*.aac" }
            : new[] { "*.mp4", "*.mov", "*.mkv", "*.webm", "*.avi", "*.m4v" };
        var path = await Dialogs.PickOpen(this, $"Import {kind.ToString().ToLowerInvariant()}", (kind.ToString(), patterns));
        if (path is null)
            return;
        var peaks = kind == TrackKind.Audio ? await Task.Run(() => WaveformReader.TryRead(path)) : null;
        var duration = await _media.ProbeDurationAsync(path) ?? peaks?.Duration;
        var track = TimelineOps.EnsureMediaTrack(_state, kind);
        var clip = new MediaClip
        {
            Name = Path.GetFileNameWithoutExtension(path),
            Path = path,
            Start = _state.Playhead,
            Duration = duration ?? 60,
            SourceDuration = duration,
        };
        _state.Waveforms[path] = peaks;
        track.Clips.Add(clip);
        track.SortClips();
        // The first song usually defines the show; stretch the show to fit it.
        if (clip.End > _state.Project.Duration)
            _state.Project.Duration = Math.Ceiling(clip.End);
        _state.Commit();
        _state.SelectClip(clip.Id);
        if (duration is null)
            _message = $"Could not read the length of {Path.GetFileName(path)}; set the clip duration in Effect Controls.";
        _timeline.Timeline.ZoomToFit();
    }

    private void LoadWaveforms()
    {
        foreach (var clip in _state.Project.AllClips.OfType<MediaClip>())
        {
            var path = clip.Path;
            if (_state.Waveforms.ContainsKey(path) || !File.Exists(path))
                continue;
            _state.Waveforms[path] = null;
            Task.Run(() => WaveformReader.TryRead(path)).ContinueWith(t =>
                Dispatcher.UIThread.Post(() =>
                {
                    _state.Waveforms[path] = t.Result;
                    _timeline.Timeline.InvalidateVisual();
                })
            );
        }
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (_closingConfirmed || !_state.Dirty)
        {
            _media.Dispose();
            _artNet?.Dispose();
            return;
        }
        e.Cancel = true;
        if (await ConfirmDiscard())
        {
            _closingConfirmed = true;
            Close();
        }
    }
}
