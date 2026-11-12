using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lampion.App.Editor;
using Lampion.App.Live;
using Lampion.Core.Model.Live;
using Lampion.Core.Persistence;

namespace Lampion.App.Views.Live;

/// <summary>
/// The set list: shows in running order, GO to cut to the selected one (the selection then steps to the next,
/// like a cue stack), Stop, and a now-playing line with time and progress.
/// </summary>
internal sealed class SetListPanel : DockPanel
{
    private readonly Window _owner;
    private readonly EditorState _state;
    private readonly LiveSession _session;
    private readonly ListBox _list = new() { MinHeight = 200 };
    private readonly CheckBox _loop = new() { Content = "Loop selected show" };
    private readonly TextBlock _now = new() { FontSize = 13, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _time = new() { FontSize = 12 };
    private readonly ProgressBar _progress = new() { Minimum = 0, Maximum = 1, Height = 6 };
    private int _shownSecond = -1;
    private bool _syncingLoop;

    /// <summary>A set list over <paramref name="state"/>'s live set, played through <paramref name="session"/>.</summary>
    public SetListPanel(Window owner, EditorState state, LiveSession session)
    {
        _owner = owner;
        _state = state;
        _session = session;
        Width = 300;
        Margin = new Thickness(0, 0, 10, 0);

        var go = new Button { Content = "GO", FontSize = 20, FontWeight = FontWeight.Bold, MinWidth = 120, Background = new SolidColorBrush(Color.FromRgb(0x2E, 0x8B, 0x57)) };
        go.Click += (_, _) => Go();
        ToolTip.SetTip(go, "Cut to the selected show; the selection moves on to the next one");
        var stop = Ui.Button("Stop show", _session.StopShow, "Stop the show; its fixtures go back to the console");

        _loop.IsCheckedChanged += (_, _) =>
        {
            if (!_syncingLoop && Selected is { } show)
            {
                show.Loop = _loop.IsChecked == true;
                _state.Commit();
            }
        };
        _list.SelectionChanged += (_, _) => SyncLoop();

        var top = Ui.Column(6,
            Ui.Section("Set list"),
            Ui.Row(
                Ui.Button("+ Show…", () => _ = AddShow(), "Add a .lampion show made on this rig"),
                Ui.Button("+ This timeline", () => LiveEdits.AddShow(_state, _state.Project.Name + " (this timeline)", ""), "Add this project's own timeline as a show"),
                Ui.Button("−", () => { if (Selected is { } s) LiveEdits.RemoveShow(_state, s); }, "Remove the selected show")
            ),
            Ui.Row(
                Ui.Button("↑", () => { if (Selected is { } s) LiveEdits.MoveShow(_state, s, -1); }, "Move up"),
                Ui.Button("↓", () => { if (Selected is { } s) LiveEdits.MoveShow(_state, s, 1); }, "Move down"),
                _loop
            )
        );
        SetDock(top, Dock.Top);
        Children.Add(top);

        var bottom = Ui.Column(6, Ui.Row(go, stop), _now, _time, _progress);
        bottom.Margin = new Thickness(0, 8, 0, 0);
        SetDock(bottom, Dock.Bottom);
        Children.Add(bottom);
        Children.Add(_list);

        session.Changed += Rebuild;
        Rebuild();
    }

    private LiveShow? Selected => _list.SelectedIndex is var i && i >= 0 && i < _state.Project.Live.Shows.Count ? _state.Project.Live.Shows[i] : null;

    /// <summary>Updates the now-playing time and progress. Call every UI tick; allocates only when the shown second changes.</summary>
    public void Refresh()
    {
        var cue = _session.Cue;
        if (cue is null || _session.Engine.ShowEnded)
        {
            _progress.Value = 0;
            if (_shownSecond != -2)
            {
                _shownSecond = -2;
                _time.Text = _session.Engine.ShowEnded ? "Finished — fixtures are back on the console." : "";
            }
            return;
        }
        var time = _session.Engine.ShowTime;
        _progress.Value = cue.Duration > 0 ? time / cue.Duration : 0;
        var second = (int)time;
        if (second != _shownSecond)
        {
            _shownSecond = second;
            _time.Text = $"{Clock(time)} / {Clock(cue.Duration)}  ·  holds {cue.OwnedCount} of {cue.Patch.FixtureCount} fixtures";
        }
    }

    private void Rebuild()
    {
        var index = _list.SelectedIndex;
        _list.ItemsSource = _state.Project.Live.Shows.Select(s => (ReferenceEquals(s, _session.Playing) ? "▶ " : "   ") + s.Name + (s.Loop ? "  ⟳" : "")).ToList();
        _list.SelectedIndex = Math.Min(Math.Max(index, 0), _state.Project.Live.Shows.Count - 1);
        _now.Text = _session.Loading ? "Loading…" : _session.Playing is { } playing ? "Now: " + playing.Name : "No show playing — everything is on the console.";
        _shownSecond = -1;
        SyncLoop();
    }

    private void SyncLoop()
    {
        _syncingLoop = true;
        _loop.IsChecked = Selected?.Loop == true;
        _syncingLoop = false;
    }

    private void Go()
    {
        if (Selected is not { } show)
            return;
        _ = _session.Go(show);
        if (_list.SelectedIndex < _state.Project.Live.Shows.Count - 1)
            _list.SelectedIndex++;
    }

    private async Task AddShow()
    {
        if (await Dialogs.PickOpen(_owner, "Add show to set list", ("Lampion show", ["*" + ProjectSerializer.Extension])) is { } path)
            LiveEdits.AddShow(_state, Path.GetFileNameWithoutExtension(path), path);
    }

    private static string Clock(double seconds) => TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(@"m\:ss");
}
