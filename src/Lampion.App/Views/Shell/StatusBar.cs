using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lampion.App.Editor;

namespace Lampion.App.Views.Shell;

/// <summary>
/// The line under the timeline: file, unsaved marker, fixture count, Art-Net state and the latest message.
/// <see cref="Refresh"/> runs every tick but only formats text when one of those changed.
/// </summary>
internal sealed class StatusBar
{
    private const string Divider = "   |   ";

    private string? _filePath;
    private bool _dirty;
    private int _fixtures = -1;
    private bool _artNet;
    private string? _message;
    private bool _stale = true;

    /// <summary>The control to place in the layout.</summary>
    public TextBlock View { get; } = new()
    {
        Foreground = Brushes.Gray,
        FontSize = 11,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(6, 3),
    };

    /// <summary>The latest outcome or problem, in plain words; null for none. Stays until replaced.</summary>
    public string? Message
    {
        get => _message;
        set
        {
            if (value == _message)
                return;
            _message = value;
            _stale = true;
        }
    }

    /// <summary>Updates the text if anything it shows changed. Allocation-free otherwise.</summary>
    /// <param name="state">The editor whose file and fixtures are shown.</param>
    /// <param name="artNetSending">True while Art-Net output is on.</param>
    public void Refresh(EditorState state, bool artNetSending)
    {
        var filePath = state.FilePath;
        var dirty = state.Dirty;
        var fixtures = state.Project.Fixtures.Count;
        if (!_stale && filePath == _filePath && dirty == _dirty && fixtures == _fixtures && artNetSending == _artNet)
            return;
        _filePath = filePath;
        _dirty = dirty;
        _fixtures = fixtures;
        _artNet = artNetSending;
        _stale = false;
        var file = filePath ?? "unsaved project";
        var art = artNetSending ? "Art-Net → broadcast" : "Art-Net off";
        View.Text = $"{file}{(dirty ? " •" : "")}{Divider}{fixtures} fixtures{Divider}{art}{(_message is null ? "" : Divider + _message)}";
    }
}
