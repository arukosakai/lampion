using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lampion.Core.Live;
using Lampion.Core.Model.Live;

namespace Lampion.App.Views.Live;

/// <summary>
/// One console fader: a vertical slider, its level in %, MIDI binding and how many of its fixtures the show holds.
/// Follows MIDI moves through <see cref="Refresh"/>; dragging writes straight to <see cref="LiveControls"/>.
/// </summary>
internal sealed class FaderStrip : Border
{
    private static readonly IBrush Held = new SolidColorBrush(Color.FromRgb(0xE0, 0x9A, 0x3A));
    private readonly LiveControls _controls;
    private readonly int _index;
    private readonly Slider _slider;
    private readonly TextBlock _percent = new() { HorizontalAlignment = HorizontalAlignment.Center, FontSize = 11 };
    private readonly TextBlock _held = new() { HorizontalAlignment = HorizontalAlignment.Center, FontSize = 10, Foreground = Held, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center };
    private bool _syncing;
    private int _shownPercent = -1;

    /// <summary>A strip for fader <paramref name="index"/>; <paramref name="edit"/> and <paramref name="remove"/> back its context menu.</summary>
    public FaderStrip(LiveFader fader, int index, LiveControls controls, Action edit, Action remove)
    {
        _controls = controls;
        _index = index;
        _slider = new Slider
        {
            Orientation = Orientation.Vertical,
            Minimum = 0,
            Maximum = 1,
            Height = 170,
            HorizontalAlignment = HorizontalAlignment.Center,
            Value = controls.Fader(index),
        };
        _slider.ValueChanged += (_, e) =>
        {
            if (!_syncing)
                _controls.SetFader(_index, e.NewValue);
        };

        var name = new TextBlock { Text = fader.Name, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        var midi = new TextBlock { Text = fader.Midi?.ToString() ?? "no MIDI", FontSize = 10, Foreground = Brushes.Gray, HorizontalAlignment = HorizontalAlignment.Center };
        ToolTip.SetTip(this, $"{fader.Name}: {fader.Target}. Right-click to edit or map MIDI.");
        ContextMenu = new ContextMenu { Items = { Item("Edit…", edit), Item("Delete", remove) } };
        Width = 84;
        Padding = new Thickness(4, 6);
        Margin = new Thickness(0, 0, 4, 0);
        Background = new SolidColorBrush(Color.FromRgb(0x24, 0x26, 0x2B));
        CornerRadius = new CornerRadius(4);
        Child = new StackPanel { Spacing = 4, Children = { name, _slider, _percent, midi, _held } };
        Refresh();
    }

    /// <summary>Shows how many of its fixtures the playing show holds; empty text hides the note.</summary>
    public void SetHeld(string? text)
    {
        _held.Text = text;
        _held.IsVisible = text is not null;
    }

    /// <summary>Follows the fader's live value (MIDI may have moved it). Allocates only when the shown % changes.</summary>
    public void Refresh()
    {
        var value = _controls.Fader(_index);
        if (Math.Abs(_slider.Value - value) > 1e-4)
        {
            _syncing = true;
            _slider.Value = value;
            _syncing = false;
        }
        var percent = (int)Math.Round(value * 100);
        if (percent != _shownPercent)
        {
            _shownPercent = percent;
            _percent.Text = percent + " %";
        }
    }

    private static MenuItem Item(string header, Action run)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => run();
        return item;
    }
}
