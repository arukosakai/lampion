using Avalonia.Controls;
using Lampion.App.Editor;

namespace Lampion.App.Views.Shell;

/// <summary>Keeps the window title as "Lampion — show name", with a dot while there are unsaved changes.</summary>
internal sealed class WindowTitle
{
    private readonly Window _window;
    private readonly EditorState _state;
    private string? _name;
    private bool _dirty;
    private bool _shown;

    /// <summary>Titles <paramref name="window"/> after <paramref name="state"/>'s project.</summary>
    public WindowTitle(Window window, EditorState state)
    {
        _window = window;
        _state = state;
    }

    /// <summary>Updates the title if the name or unsaved state changed. Allocation-free otherwise; call every tick.</summary>
    public void Refresh()
    {
        var name = _state.Project.Name;
        var dirty = _state.Dirty;
        if (_shown && name == _name && dirty == _dirty)
            return;
        _name = name;
        _dirty = dirty;
        _shown = true;
        _window.Title = $"Lampion — {name}{(dirty ? " •" : "")}";
    }
}
