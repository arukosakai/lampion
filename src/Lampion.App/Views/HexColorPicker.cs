using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Lampion.Core.Color;

namespace Lampion.App.Views;

/// <summary>
/// The colour picker behind <see cref="Ui.ColorPicker"/>: a swatch button whose flyout has HSB sliders,
/// a hex box and preset chips, all kept in step with each other.
/// </summary>
internal sealed class HexColorPicker
{
    private static readonly string[] Presets = ["#FFFFFF", "#FFE6B0", "#FF0000", "#FF6A00", "#FFC800", "#00FF00", "#00FFC8", "#00A0FF", "#0000FF", "#8000FF", "#FF00C8", "#000000"];

    private readonly Action<string> _onPreview;
    private readonly Border _swatch;
    private readonly TextBlock _hexLabel;
    private readonly TextBox _hexBox;
    private readonly Slider _hue;
    private readonly Slider _saturation;
    private readonly Slider _brightness;
    private string _hex;
    // Set while sliders are moved from code, so their change events don't echo back as user edits.
    private bool _updating;

    /// <summary>A picker starting at <paramref name="hex"/>; see <see cref="Ui.ColorPicker"/> for the callbacks.</summary>
    public HexColorPicker(string hex, Action<string> onPreview, Action onCommit)
    {
        _hex = hex;
        _onPreview = onPreview;
        _swatch = new Border
        {
            Width = 44,
            Height = 16,
            CornerRadius = new CornerRadius(2),
            Background = Ui.Brush(hex),
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(1),
        };
        _hexLabel = new TextBlock { Text = hex.ToUpperInvariant(), VerticalAlignment = VerticalAlignment.Center };
        Button = new Button { Content = Ui.Row(_swatch, _hexLabel), HorizontalAlignment = HorizontalAlignment.Left };

        var (h, s, v) = Rgb.FromHex(hex).ToHsv();
        _hexBox = new TextBox { Text = hex, Width = 90 };
        _hue = Ui.Slider(h * 360, 0, 360, _ => FromSliders());
        _saturation = Ui.Slider(s, 0, 1, _ => FromSliders());
        _brightness = Ui.Slider(v, 0, 1, _ => FromSliders());
        _hexBox.TextChanged += (_, _) => FromHexBox();

        var flyout = new Flyout
        {
            Content = Ui.Column(4, Ui.Field("Hue", _hue, 70), Ui.Field("Saturation", _saturation, 70), Ui.Field("Brightness", _brightness, 70), Ui.Field("Hex", _hexBox, 70), PresetChips()),
            Placement = PlacementMode.BottomEdgeAlignedLeft,
        };
        flyout.Closed += (_, _) => onCommit();
        Button.Flyout = flyout;
    }

    /// <summary>The swatch button to place in a panel.</summary>
    public Button Button { get; }

    private WrapPanel PresetChips()
    {
        var presets = new WrapPanel { MaxWidth = 230 };
        foreach (var preset in Presets)
        {
            var chip = new Button
            {
                Width = 18,
                Height = 18,
                MinHeight = 18,
                Padding = new Thickness(0),
                Margin = new Thickness(1),
                Background = Ui.Brush(preset),
            };
            chip.Click += (_, _) =>
            {
                SetSliders(Rgb.FromHex(preset));
                Apply(preset);
            };
            presets.Children.Add(chip);
        }
        return presets;
    }

    private void FromSliders()
    {
        if (!_updating)
            Apply(Rgb.FromHsv(_hue.Value / 360, _saturation.Value, _brightness.Value).ToHex());
    }

    private void FromHexBox()
    {
        if (!Rgb.TryParseHex(_hexBox.Text, out var parsed) || parsed.ToHex() == _hex)
            return;
        SetSliders(parsed);
        Apply(parsed.ToHex(), fromHexBox: true);
    }

    private void SetSliders(Rgb color)
    {
        _updating = true;
        var hsv = color.ToHsv();
        _hue.Value = hsv.H * 360;
        _saturation.Value = hsv.S;
        _brightness.Value = hsv.V;
        _updating = false;
    }

    private void Apply(string newHex, bool fromHexBox = false)
    {
        _hex = newHex.ToUpperInvariant();
        _swatch.Background = Ui.Brush(_hex);
        _hexLabel.Text = _hex;
        if (!fromHexBox)
            _hexBox.Text = _hex;
        _onPreview(_hex);
    }
}
