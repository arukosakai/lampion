namespace Lampion.App.Views.Shell;

/// <summary>One user action of the workspace, shared by the menu bar and the shortcut map.</summary>
internal sealed class ShellCommand
{
    private readonly Action _run;

    /// <summary>A command shown as <paramref name="header"/> that runs <paramref name="run"/>.</summary>
    /// <param name="header">Menu text; "…" marks commands that open a dialog first.</param>
    /// <param name="run">The action. Async work is fired and forgotten; it reports its own errors.</param>
    /// <param name="gesture">The shortcut shown in the menu, in <see cref="Avalonia.Input.KeyGesture.Parse"/> syntax; null for none. Display only: <see cref="ShortcutMap"/> does the handling.</param>
    public ShellCommand(string header, Action run, string? gesture = null)
    {
        Header = header;
        Gesture = gesture;
        _run = run;
    }

    /// <summary>Menu text.</summary>
    public string Header { get; }

    /// <summary>The shortcut shown next to the menu item, or null.</summary>
    public string? Gesture { get; }

    /// <summary>For toggles, reads whether the command is on; the menu shows it as a check box. Null for plain commands.</summary>
    public Func<bool>? IsChecked { get; init; }

    /// <summary>Runs the command.</summary>
    public void Run() => _run();
}
