using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lampion.App.Editor;
using Lampion.App.Live;
using Lampion.Core.Model.Live;
using Lampion.Core.Model.Timeline;
using Lampion.Core.Persistence;

namespace Lampion.App.Views.Live;

/// <summary>
/// The set list: shows in running order, GO to cut to the selected one (the selection then steps to the next,
/// like a cue stack), Stop, a now-playing line with time and progress, and the MIDI buttons for GO, Stop and each show.
/// The selection is the session's <see cref="LiveSession.Next"/>, so a MIDI GO moves it too.
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
    private readonly TextBlock _music = new() { FontSize = 11, Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap };
    private readonly ContentControl _showMidi = new();
    private LiveShow? _showMidiFor;
    private bool _syncingSelection;
    private int _shownErrorMs = int.MinValue;
    private MediaClip? _shownMusic;
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
        _list.SelectionChanged += (_, _) =>
        {
            if (!_syncingSelection)
                _session.Next = Selected;
            SyncLoop();
        };

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

        var delay = Ui.Number(state.Project.Live.LightDelayMs, v => { _state.Project.Live.LightDelayMs = v; _state.Commit(); }, -500, 500, 5, "0");
        ToolTip.SetTip(delay, "Milliseconds the lights wait for the sound: raise it until hits land with the kick through the PA (negative runs them early)");
        var midi = new Expander
        {
            Header = "MIDI buttons",
            Content = Ui.Column(4,
                MidiLearnRow.Build(_session, () => _state.Project.Live.GoMidi, b => { _state.Project.Live.GoMidi = b; _state.Commit(); }, "GO"),
                MidiLearnRow.Build(_session, () => _state.Project.Live.StopMidi, b => { _state.Project.Live.StopMidi = b; _state.Commit(); }, "Stop"),
                _showMidi),
        };
        ToolTip.SetTip(midi, "Controller buttons for GO, Stop and the selected show; they light up while it plays");
        var bottom = Ui.Column(6, Ui.Row(go, stop), _now, _time, _progress, _music, Ui.Field("Light delay (ms)", delay, 110), midi);
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
        ShowMusic();
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

    // Which song the lights follow and how far off they were at the last correction; text only changes when the whole ms does.
    private void ShowMusic()
    {
        var clip = _session.FollowingMusic;
        var errorMs = clip is null || _session.Cue is not { } cue ? 0 : (int)Math.Round(cue.Clock.LastError * 1000);
        if (ReferenceEquals(clip, _shownMusic) && errorMs == _shownErrorMs)
            return;
        _shownMusic = clip;
        _shownErrorMs = errorMs;
        _music.Text = clip is null
            ? (_session.Cue is null ? "" : "No music playing: the lights run on their own clock.")
            : $"♪ {Path.GetFileName(clip.Path)} · lights follow the music (last correction {errorMs:+0;-0;0} ms)";
    }

    private void Rebuild()
    {
        var index = _list.SelectedIndex;
        var shows = _state.Project.Live.Shows;
        _syncingSelection = true;
        _list.ItemsSource = shows.Select(s => (ReferenceEquals(s, _session.Playing) ? "▶ " : "   ") + s.Name + (s.Loop ? "  ⟳" : "")).ToList();
        var next = _session.Next is { } n ? shows.IndexOf(n) : -1;
        _list.SelectedIndex = next >= 0 ? next : Math.Min(Math.Max(index, 0), shows.Count - 1);
        _syncingSelection = false;
        _session.Next = Selected;
        _now.Text = _session.Loading ? "Loading…" : _session.Playing is { } playing ? "Now: " + playing.Name : "No show playing — everything is on the console.";
        _shownSecond = -1;
        SyncLoop();
    }

    private void SyncLoop()
    {
        _syncingLoop = true;
        _loop.IsChecked = Selected?.Loop == true;
        _syncingLoop = false;
        if (ReferenceEquals(Selected, _showMidiFor))
            return;
        _showMidiFor = Selected;
        _showMidi.Content = Selected is { } show
            ? MidiLearnRow.Build(_session, () => show.Midi, b => { show.Midi = b; _state.Commit(); }, "This show")
            : null;
    }

    private void Go()
    {
        if (Selected is not null)
            _session.GoNext();
    }

    private async Task AddShow()
    {
        if (await Dialogs.PickOpen(_owner, "Add show to set list", ("Lampion show", ["*" + ProjectSerializer.Extension])) is { } path)
            LiveEdits.AddShow(_state, Path.GetFileNameWithoutExtension(path), path);
    }

    private static string Clock(double seconds) => TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(@"m\:ss");
}
