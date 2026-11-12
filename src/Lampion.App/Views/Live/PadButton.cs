using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Lampion.App.Live;
using Lampion.Core.Model.Live;

namespace Lampion.App.Views.Live;

/// <summary>
/// A launchpad-style pad: lights up in its effect's colour while on. Left press/release go to the session as a
/// MIDI note would (so flash pads work with the mouse too); right-click opens the menu.
/// </summary>
internal sealed class PadButton : Border
{
    private static readonly IBrush Off = new SolidColorBrush(Color.FromRgb(0x2A, 0x2C, 0x31));
    private readonly LiveSession _session;
    private readonly int _index;
    private readonly IBrush _lit;
    private bool _shownOn;

    /// <summary>A pad for pad <paramref name="index"/>; <paramref name="edit"/> and <paramref name="remove"/> back its context menu.</summary>
    public PadButton(LivePad pad, int index, LiveSession session, Action edit, Action remove)
    {
        _session = session;
        _index = index;
        _lit = Ui.Brush(pad.Effect.SwatchColor);
        Width = 96;
        Height = 72;
        Margin = new Thickness(0, 0, 6, 6);
        CornerRadius = new CornerRadius(6);
        BorderThickness = new Thickness(1);
        BorderBrush = Brushes.Black;
        Background = Off;
        Cursor = new Cursor(StandardCursorType.Hand);
        Child = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock { Text = pad.Name, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis },
                new TextBlock { Text = (pad.Mode == PadMode.Flash ? "flash · " : "") + (pad.Midi?.ToString() ?? "no MIDI"), FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center },
            },
        };
        ToolTip.SetTip(this, $"{pad.Name}: {pad.Effect.DisplayName}. Click to {(pad.Mode == PadMode.Flash ? "flash" : "start / stop")}; right-click to edit or map MIDI.");
        var editItem = new MenuItem { Header = "Edit…" };
        editItem.Click += (_, _) => edit();
        var removeItem = new MenuItem { Header = "Delete" };
        removeItem.Click += (_, _) => remove();
        ContextMenu = new ContextMenu { Items = { editItem, removeItem } };
    }

    /// <summary>Lights the pad to match its state. Changes the brush only when the state flips.</summary>
    public void Refresh()
    {
        var on = _session.Controls.IsPadOn(_index);
        if (on == _shownOn)
            return;
        _shownOn = on;
        Background = on ? _lit : Off;
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        e.Pointer.Capture(this);
        _session.PressPad(_index);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton != MouseButton.Left)
            return;
        _session.ReleasePad(_index);
        e.Pointer.Capture(null);
        e.Handled = true;
    }
}
