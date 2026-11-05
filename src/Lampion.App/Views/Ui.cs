using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Lampion.Core.Color;

namespace Lampion.App.Views;

/// <summary>Small factory helpers so the code-built panels stay readable.</summary>
internal static class Ui
{
    public static TextBlock Label(string text, string? cls = "label")
    {
        var block = new TextBlock { Text = text };
        if (cls is not null)
            block.Classes.Add(cls);
        return block;
    }

    public static TextBlock Section(string text) => Label(text, "section");

    public static Button Button(string text, Action onClick, string? tip = null)
    {
        var button = new Button { Content = text };
        button.Click += (_, _) => onClick();
        if (tip is not null)
            ToolTip.SetTip(button, tip);
        return button;
    }

    public static ToggleButton Toggle(string text, bool initial, Action<bool> onChange, string? tip = null)
    {
        var toggle = new ToggleButton { Content = text, IsChecked = initial };
        toggle.IsCheckedChanged += (_, _) => onChange(toggle.IsChecked == true);
        if (tip is not null)
            ToolTip.SetTip(toggle, tip);
        return toggle;
    }

    public static StackPanel Row(params Control[] children)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        row.Children.AddRange(children);
        return row;
    }

    public static StackPanel Column(double spacing = 4, params Control[] children)
    {
        var column = new StackPanel { Orientation = Orientation.Vertical, Spacing = spacing };
        column.Children.AddRange(children);
        return column;
    }

    /// <summary>A label-on-the-left property row, the After Effects "Effect Controls" layout.</summary>
    public static Grid Field(string label, Control editor, double labelWidth = 92)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions($"{labelWidth.ToString(CultureInfo.InvariantCulture)},*"), Margin = new Thickness(0, 1) };
        var text = Label(label);
        grid.Children.Add(text);
        Grid.SetColumn(editor, 1);
        grid.Children.Add(editor);
        return grid;
    }

    /// <summary>Numeric editor that commits on Enter/blur and steps with the spinner.</summary>
    public static NumericUpDown Number(double value, Action<double> onCommit, double min = -10000, double max = 10000, double step = 0.1, string format = "0.###")
    {
        var box = new NumericUpDown
        {
            Value = (decimal)Math.Clamp(value, min, max),
            Minimum = (decimal)min,
            Maximum = (decimal)max,
            Increment = (decimal)step,
            FormatString = format,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ClipValueToMinMax = true,
        };
        box.ValueChanged += (_, e) =>
        {
            if (e.NewValue is { } v && e.NewValue != e.OldValue)
                onCommit((double)v);
        };
        return box;
    }

    public static TextBox Text(string value, Action<string> onCommit)
    {
        var box = new TextBox { Text = value };
        box.LostFocus += (_, _) =>
        {
            if (box.Text != value)
            {
                value = box.Text ?? "";
                onCommit(value);
            }
        };
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter && box.Text != value)
            {
                value = box.Text ?? "";
                onCommit(value);
            }
        };
        return box;
    }

    public static ComboBox Combo<T>(IEnumerable<T> items, T selected, Action<T> onChange, Func<T, string>? display = null)
    {
        var list = items.ToList();
        var combo = new ComboBox
        {
            ItemsSource = list.Select(i => display is null ? i?.ToString() ?? "" : display(i)).ToList(),
            SelectedIndex = list.IndexOf(selected),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedIndex >= 0 && combo.SelectedIndex < list.Count)
                onChange(list[combo.SelectedIndex]);
        };
        return combo;
    }

    public static Slider Slider(double value, double min, double max, Action<double> onChange, Action? onRelease = null)
    {
        var slider = new Slider { Minimum = min, Maximum = max, Value = value, HorizontalAlignment = HorizontalAlignment.Stretch };
        slider.ValueChanged += (_, e) => onChange(e.NewValue);
        if (onRelease is not null)
            slider.AddHandler(InputElement.PointerCaptureLostEvent, (_, _) => onRelease(), handledEventsToo: true);
        return slider;
    }

    public static IBrush Brush(string hex)
    {
        var c = Rgb.FromHex(hex);
        return new SolidColorBrush(Color.FromRgb(Rgb.ToByte(c.R), Rgb.ToByte(c.G), Rgb.ToByte(c.B)));
    }

    public static Color ToColor(string hex, byte alpha = 255)
    {
        var c = Rgb.FromHex(hex);
        return Color.FromArgb(alpha, Rgb.ToByte(c.R), Rgb.ToByte(c.G), Rgb.ToByte(c.B));
    }

    /// <summary>
    /// A colour swatch that opens a flyout with hue/saturation/brightness sliders, a hex box and presets.
    /// <paramref name="onPreview"/> fires while dragging; <paramref name="onCommit"/> when the flyout closes.
    /// </summary>
    public static Button ColorPicker(string hex, Action<string> onPreview, Action onCommit)
    {
        var swatch = new Border
        {
            Width = 44,
            Height = 16,
            CornerRadius = new CornerRadius(2),
            Background = Brush(hex),
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(1),
        };
        var hexLabel = new TextBlock { Text = hex.ToUpperInvariant(), VerticalAlignment = VerticalAlignment.Center };
        var button = new Button { Content = Row(swatch, hexLabel), HorizontalAlignment = HorizontalAlignment.Left };

        var (h, s, v) = Rgb.FromHex(hex).ToHsv();
        var updating = false;
        var hexBox = new TextBox { Text = hex, Width = 90 };
        Slider? hue = null, sat = null, val = null;
        void Apply(string newHex, bool fromHexBox = false)
        {
            hex = newHex.ToUpperInvariant();
            swatch.Background = Brush(hex);
            hexLabel.Text = hex;
            if (!fromHexBox)
                hexBox.Text = hex;
            onPreview(hex);
        }
        void FromSliders()
        {
            if (!updating && hue is not null && sat is not null && val is not null)
                Apply(Rgb.FromHsv(hue.Value / 360, sat.Value, val.Value).ToHex());
        }
        hue = Slider(h * 360, 0, 360, _ => FromSliders());
        sat = Slider(s, 0, 1, _ => FromSliders());
        val = Slider(v, 0, 1, _ => FromSliders());
        hexBox.TextChanged += (_, _) =>
        {
            if (!Rgb.TryParseHex(hexBox.Text, out var parsed) || parsed.ToHex() == hex)
                return;
            updating = true;
            var hsv = parsed.ToHsv();
            hue.Value = hsv.H * 360;
            sat.Value = hsv.S;
            val.Value = hsv.V;
            updating = false;
            Apply(parsed.ToHex(), fromHexBox: true);
        };

        var presets = new WrapPanel { MaxWidth = 230 };
        foreach (var preset in new[] { "#FFFFFF", "#FFE6B0", "#FF0000", "#FF6A00", "#FFC800", "#00FF00", "#00FFC8", "#00A0FF", "#0000FF", "#8000FF", "#FF00C8", "#000000" })
        {
            var chip = new Button
            {
                Width = 18,
                Height = 18,
                MinHeight = 18,
                Padding = new Thickness(0),
                Margin = new Thickness(1),
                Background = Brush(preset),
            };
            chip.Click += (_, _) =>
            {
                updating = true;
                var hsv = Rgb.FromHex(preset).ToHsv();
                hue.Value = hsv.H * 360;
                sat.Value = hsv.S;
                val.Value = hsv.V;
                updating = false;
                Apply(preset);
            };
            presets.Children.Add(chip);
        }

        var flyout = new Flyout
        {
            Content = Column(4, Field("Hue", hue, 70), Field("Saturation", sat, 70), Field("Brightness", val, 70), Field("Hex", hexBox, 70), presets),
            Placement = PlacementMode.BottomEdgeAlignedLeft,
        };
        flyout.Closed += (_, _) => onCommit();
        button.Flyout = flyout;
        return button;
    }
}
