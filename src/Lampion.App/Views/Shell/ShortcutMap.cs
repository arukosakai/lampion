using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Lampion.App.Views.Shell;

/// <summary>
/// The workspace's keyboard shortcuts, After Effects style, as one table. Keys are caught on the tunnel
/// pass so panels can't swallow them; text boxes keep their typing keys and Ctrl+C/V.
/// </summary>
internal sealed class ShortcutMap
{
    private readonly Shortcut[] _table;

    /// <summary>Binds the shortcuts to <paramref name="c"/>.</summary>
    public ShortcutMap(EditorCommands c)
    {
        // First match wins, so a Shift row must come before the same key without Shift.
        _table =
        [
            new(Key.Z, Ctrl: true, Shift: true, c.Redo),
            new(Key.Z, Ctrl: true, Shift: false, c.Undo),
            new(Key.Y, Ctrl: true, Shift: false, c.Redo),
            new(Key.S, Ctrl: true, Shift: true, c.SaveAs),
            new(Key.S, Ctrl: true, Shift: false, c.Save),
            new(Key.O, Ctrl: true, Shift: false, c.Open),
            new(Key.N, Ctrl: true, Shift: false, c.NewProject),
            new(Key.P, Ctrl: true, Shift: true, c.ProjectSettings),
            new(Key.E, Ctrl: true, Shift: false, c.ExportQlc),
            new(Key.L, Ctrl: true, Shift: false, c.LiveConsole),
            new(Key.I, Ctrl: true, Shift: false, c.ImportAudio),
            new(Key.K, Ctrl: true, Shift: false, c.SplitAtPlayhead),
            new(Key.D, Ctrl: true, Shift: false, c.Duplicate),
            new(Key.C, Ctrl: true, Shift: false, c.Copy, YieldsToText: true),
            new(Key.V, Ctrl: true, Shift: false, c.Paste, YieldsToText: true),

            new(Key.Space, Ctrl: false, Shift: false, c.PlayPause),
            new(Key.Home, Ctrl: false, Shift: false, c.GoToStart),
            new(Key.End, Ctrl: false, Shift: false, c.GoToEnd),
            new(Key.Delete, Ctrl: false, Shift: false, c.Delete),
            new(Key.Back, Ctrl: false, Shift: false, c.Delete),
            new(Key.M, Ctrl: false, Shift: false, c.AddMarker),
            new(Key.OemPlus, Ctrl: false, Shift: false, c.ZoomIn),
            new(Key.Add, Ctrl: false, Shift: false, c.ZoomIn),
            new(Key.OemMinus, Ctrl: false, Shift: false, c.ZoomOut),
            new(Key.Subtract, Ctrl: false, Shift: false, c.ZoomOut),
            new(Key.OemPipe, Ctrl: false, Shift: false, c.ZoomToFit),
            new(Key.OemBackslash, Ctrl: false, Shift: false, c.ZoomToFit),
            new(Key.Left, Ctrl: false, Shift: true, c.StepBackSecond),
            new(Key.Left, Ctrl: false, Shift: false, c.StepBack),
            new(Key.Right, Ctrl: false, Shift: true, c.StepForwardSecond),
            new(Key.Right, Ctrl: false, Shift: false, c.StepForward),
            new(Key.D1, Ctrl: false, Shift: false, c.PerspectiveView),
            new(Key.D2, Ctrl: false, Shift: false, c.TopView),
            new(Key.D3, Ctrl: false, Shift: false, c.FrontView),
            new(Key.D4, Ctrl: false, Shift: false, c.RightView),
            new(Key.D5, Ctrl: false, Shift: false, c.ToggleProjection),
            new(Key.Q, Ctrl: false, Shift: false, c.HandTool),
            new(Key.W, Ctrl: false, Shift: false, c.MoveTool),
            new(Key.A, Ctrl: false, Shift: false, c.AimTool),
            new(Key.F, Ctrl: false, Shift: false, c.FrameSelection),
            new(Key.Escape, Ctrl: false, Shift: false, c.Deselect),
        ];
    }

    /// <summary>The shortcut table, in matching order.</summary>
    public IReadOnlyList<Shortcut> Shortcuts => _table;

    /// <summary>Starts handling key presses anywhere in <paramref name="window"/>.</summary>
    public void Attach(TopLevel window) =>
        window.AddHandler(InputElement.KeyDownEvent, (_, e) => Handle(e, window.FocusManager?.GetFocusedElement() is TextBox), RoutingStrategies.Tunnel);

    /// <summary>Runs the command bound to the key press, if any, and marks the event handled.</summary>
    /// <param name="e">The key press.</param>
    /// <param name="typing">True while a text box has focus.</param>
    /// <returns>True when a command ran.</returns>
    public bool Handle(KeyEventArgs e, bool typing)
    {
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        // Let text fields keep their keys; only chorded shortcuts pass through them.
        if (typing && !ctrl)
            return false;
        foreach (var shortcut in _table)
        {
            if (shortcut.Key != e.Key || shortcut.Ctrl != ctrl || (shortcut.Shift && !shift) || (typing && shortcut.YieldsToText))
                continue;
            shortcut.Command.Run();
            e.Handled = true;
            return true;
        }
        return false;
    }
}
