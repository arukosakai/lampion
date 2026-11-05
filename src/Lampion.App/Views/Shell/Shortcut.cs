using Avalonia.Input;

namespace Lampion.App.Views.Shell;

/// <summary>One row of the shortcut table: a key, the modifiers it needs, and the command it runs.</summary>
/// <param name="Key">The key pressed.</param>
/// <param name="Ctrl">True when Ctrl (Cmd on macOS) must be held; false when it must not be.</param>
/// <param name="Shift">True when Shift must be held; false when Shift doesn't matter.</param>
/// <param name="Command">What runs.</param>
/// <param name="YieldsToText">True to leave the key to a focused text box (Ctrl+C/V). Unchorded shortcuts always do.</param>
internal readonly record struct Shortcut(Key Key, bool Ctrl, bool Shift, ShellCommand Command, bool YieldsToText = false);
