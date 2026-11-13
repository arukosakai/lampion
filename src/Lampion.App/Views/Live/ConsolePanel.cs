using Avalonia;
using Avalonia.Controls;
using Lampion.App.Editor;
using Lampion.App.Live;
using Lampion.Core.Live;
using Lampion.Core.Midi;
using Lampion.Core.Model.Live;

namespace Lampion.App.Views.Live;

/// <summary>
/// The virtual console: fader strips in a row, effect pads in a grid. Rebuilt when the patch is, refreshed every
/// UI tick to follow MIDI and pad states, and marks faders whose fixtures the playing show holds.
/// </summary>
internal sealed class ConsolePanel : DockPanel
{
    private readonly Window _owner;
    private readonly EditorState _state;
    private readonly LiveSession _session;
    private readonly StackPanel _faders = new() { Orientation = Avalonia.Layout.Orientation.Horizontal };
    private readonly WrapPanel _pads = new();
    private readonly List<FaderStrip> _strips = [];
    private readonly List<PadButton> _buttons = [];
    private readonly Button _mapApc;
    private ShowCue? _heldFor;
    private bool _heldEnded;

    /// <summary>A console over <paramref name="session"/>, editing <paramref name="state"/>'s live set.</summary>
    public ConsolePanel(Window owner, EditorState state, LiveSession session)
    {
        _owner = owner;
        _state = state;
        _session = session;
        var addFader = Ui.Button("+ Fader", () => _ = FaderEditor.Show(_owner, _state, _session, LiveEdits.AddFader(_state)), "Add a fader over the fixtures selected in the workspace");
        var addPad = Ui.Button("+ Pad", () => _ = PadEditor.Show(_owner, _state, _session, LiveEdits.AddPad(_state)), "Add an effect pad over the fixtures selected in the workspace");
        var save = Ui.Button("Save console…", () => _ = ConsoleFiles.Save(_owner, _state), "Save this console (set list, faders, pads, MIDI mappings, widgets) to a file you can load into another project");
        var load = Ui.Button("Load console…", () => _ = ConsoleFiles.Load(_owner, _state), "Replace this console with one saved from another project; its fixtures are matched by address, groups by name");
        _mapApc = Ui.Button("Map to APC mini mk2", () => LiveEdits.MapToApcMiniMk2(_state),
            "Bind faders 1–9 to its faders, the first 64 pads to its grid (top row first), shows 1–7 to its scene buttons, Stop to the bottom scene button and GO to the rightmost track button. Undoable.");
        var bar = Ui.Row(Ui.Section("Console"), addFader, addPad, save, load, _mapApc);
        bar.Margin = new Thickness(0, 0, 0, 6);
        SetDock(bar, Dock.Top);
        Children.Add(bar);

        var padsHeader = Ui.Section("Pads");
        padsHeader.Margin = new Thickness(0, 10, 0, 4);
        Children.Add(new ScrollViewer
        {
            Content = new StackPanel
            {
                Children =
                {
                    new ScrollViewer { Content = _faders, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto },
                    padsHeader,
                    _pads,
                },
            },
        });
        session.Changed += Rebuild;
        Rebuild();
    }

    /// <summary>Follows MIDI-moved faders, lit pads and the show's hold on fixtures. Call every UI tick.</summary>
    public void Refresh()
    {
        foreach (var strip in _strips)
            strip.Refresh();
        foreach (var button in _buttons)
            button.Refresh();
        if (!ReferenceEquals(_session.Cue, _heldFor) || _session.Engine.ShowEnded != _heldEnded)
            ShowHeld();
        _mapApc.IsVisible = ApcMiniMk2.Matches(_session.MidiIn?.Name);
    }

    private void Rebuild()
    {
        var live = _state.Project.Live;
        _faders.Children.Clear();
        _strips.Clear();
        for (var i = 0; i < live.Faders.Count && i < LiveControls.MaxFaders; i++)
        {
            var fader = live.Faders[i];
            var strip = new FaderStrip(fader, i, _session.Controls, () => _ = FaderEditor.Show(_owner, _state, _session, fader), () => LiveEdits.RemoveFader(_state, fader));
            _strips.Add(strip);
            _faders.Children.Add(strip);
        }
        if (live.Faders.Count == 0)
            _faders.Children.Add(Ui.Label("No faders yet. Select fixtures in the workspace (e.g. the reflectors) and press + Fader.", null));

        _pads.Children.Clear();
        _buttons.Clear();
        for (var i = 0; i < live.Pads.Count && i < LiveControls.MaxPads; i++)
        {
            var pad = live.Pads[i];
            var button = new PadButton(pad, i, _session, () => _ = PadEditor.Show(_owner, _state, _session, pad), () => LiveEdits.RemovePad(_state, pad));
            _buttons.Add(button);
            _pads.Children.Add(button);
        }
        if (live.Pads.Count == 0)
            _pads.Children.Add(Ui.Label("No pads yet. + Pad adds a rainbow you can start and stop.", null));
        ShowHeld();
    }

    private void ShowHeld()
    {
        _heldFor = _session.Cue;
        _heldEnded = _session.Engine.ShowEnded;
        var cue = _heldEnded ? null : _heldFor;
        var patch = _session.Patch;
        for (var i = 0; i < _strips.Count; i++)
        {
            if (cue is null || patch is null || i >= patch.FaderCount)
            {
                _strips[i].SetHeld(null);
                continue;
            }
            var targets = patch.FaderFixtures(i);
            var held = 0;
            foreach (var fixture in targets)
                if (cue.Owns(fixture))
                    held++;
            _strips[i].SetHeld(held == 0 ? null : held == targets.Length ? "all held by show" : $"{held}/{targets.Length} held by show");
        }
    }
}
