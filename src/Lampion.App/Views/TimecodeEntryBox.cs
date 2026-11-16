using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Lampion.App.Controls;
using Lampion.App.Controls.Timeline;
using Lampion.App.Editor;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Views;

/// <summary>
/// The box that replaces the timeline's timecode readout while you type a time to go to, like clicking
/// the current time in After Effects. Enter seeks, Escape or clicking away cancels.
/// </summary>
internal sealed class TimecodeEntryBox : TextBox
{
    private const string Hint = "Type a time: 1:30, 00:01:30:15, 90.5, or +5 / -2 to move from here";
    private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0x50, 0x50));

    private readonly EditorState _state;

    /// <summary>A hidden box that seeks <paramref name="state"/>'s playhead; overlay it on the timeline's top-left corner.</summary>
    public TimecodeEntryBox(EditorState state)
    {
        _state = state;
        IsVisible = false;
        Width = TimelineControl.HeaderWidth - 8;
        Height = TimelineControl.RulerHeight - 4;
        MinHeight = 0;
        Margin = new Thickness(4, 2);
        Padding = new Thickness(6, 2);
        FontSize = 14;
        FontWeight = FontWeight.SemiBold;
        VerticalContentAlignment = VerticalAlignment.Center;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        ToolTip.SetTip(this, Hint);
        TextChanged += (_, _) => ClearError();
    }

    /// <inheritdoc />
    /// <remarks>Without this a subclass gets no theme at all: no template, so nothing is drawn.</remarks>
    protected override Type StyleKeyOverride => typeof(TextBox);

    /// <summary>Shows the box with the playhead's timecode selected, ready to type over.</summary>
    public void Open()
    {
        Text = TimelineControl.Timecode(_state.Playhead);
        ClearError();
        IsVisible = true;
        Focus();
        SelectAll();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                Commit();
                e.Handled = true;
                return;
            case Key.Escape:
                Close();
                e.Handled = true;
                return;
        }
        base.OnKeyDown(e);
    }

    /// <inheritdoc />
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        IsVisible = false;
    }

    private void Commit()
    {
        if (!TimecodeParser.TryParse(Text, _state.Playhead, TimecodeText.DefaultFps, out var seconds))
        {
            BorderBrush = ErrorBrush;
            ToolTip.SetTip(this, "That isn't a time. " + Hint);
            ToolTip.SetIsOpen(this, true);
            return;
        }
        _state.Seek(seconds);
        Close();
    }

    private void Close()
    {
        IsVisible = false;
        // Hand focus back to the window so Space and the arrow keys drive the transport again.
        TopLevel.GetTopLevel(this)?.Focus();
    }

    private void ClearError()
    {
        ClearValue(BorderBrushProperty);
        ToolTip.SetIsOpen(this, false);
        ToolTip.SetTip(this, Hint);
    }
}
