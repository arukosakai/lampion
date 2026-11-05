using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Lampion.Core.Color;

namespace Lampion.App.Views;

/// <summary>
/// Small factory helpers so the code-built panels stay readable. Each call returns a new control wired to
/// its callbacks; callbacks run on the UI thread.
/// </summary>
internal static class Ui
{
    /// <summary>A text label with style class <paramref name="cls"/> ("label" by default; null for none).</summary>
    public static TextBlock Label(string text, string? cls = "label")
    {
        var block = new TextBlock { Text = text };
        if (cls is not null)
            block.Classes.Add(cls);
        return block;
    }

    /// <summary>A section heading in the accent colour, like Effect Controls' group titles.</summary>
    public static TextBlock Section(string text) => Label(text, "section");

    /// <summary>A push button running <paramref name="onClick"/>, with an optional tooltip.</summary>
    public static Button Button(string text, Action onClick, string? tip = null)
    {
        var button = new Button { Content = text };
        button.Click += (_, _) => onClick();
        if (tip is not null)
            ToolTip.SetTip(button, tip);
        return button;
    }

    /// <summary>A toggle button; <paramref name="onChange"/> gets the new state on every change.</summary>
    public static ToggleButton Toggle(string text, bool initial, Action<bool> onChange, string? tip = null)
    {
        var toggle = new ToggleButton { Content = text, IsChecked = initial };
        toggle.IsCheckedChanged += (_, _) => onChange(toggle.IsChecked == true);
        if (tip is not null)
            ToolTip.SetTip(toggle, tip);
        return toggle;
    }

    /// <summary>Controls side by side, 4 px apart.</summary>
    public static StackPanel Row(params Control[] children)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        row.Children.AddRange(children);
        return row;
    }

    /// <summary>Controls stacked top to bottom, <paramref name="spacing"/> px apart.</summary>
    public static StackPanel Column(double spacing = 4, params Control[] children)
    {
        var column = new StackPanel { Orientation = Orientation.Vertical, Spacing = spacing };
        column.Children.AddRange(children);
        return column;
    }

    /// <summary>A label-on-the-left property row, the After Effects "Effect Controls" layout.</summary>
    /// <param name="label">The property name.</param>
    /// <param name="editor">The value editor, placed in the second column.</param>
    /// <param name="labelWidth">Width of the label column, in pixels.</param>
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
    /// <param name="value">Initial value, clamped into range.</param>
    /// <param name="onCommit">Gets each new value.</param>
    /// <param name="min">Smallest value allowed.</param>
    /// <param name="max">Largest value allowed.</param>
    /// <param name="step">Spinner increment.</param>
    /// <param name="format">.NET number format for the text, e.g. "0.##".</param>
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

    /// <summary>A text box that commits on Enter or when it loses focus, and only when the text changed.</summary>
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
            if (e.Key == Key.Enter && box.Text != value)
            {
                value = box.Text ?? "";
                onCommit(value);
            }
        };
        return box;
    }

    /// <summary>A drop-down over <paramref name="items"/>; nothing is selected when <paramref name="selected"/> isn't among them.</summary>
    /// <param name="items">The choices, in display order.</param>
    /// <param name="selected">The initial choice.</param>
    /// <param name="onChange">Gets the item the user picks.</param>
    /// <param name="display">Turns an item into its text; <see cref="object.ToString"/> when null.</param>
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

    /// <summary>A horizontal slider; <paramref name="onChange"/> fires while dragging, <paramref name="onRelease"/> once the pointer lets go.</summary>
    public static Slider Slider(double value, double min, double max, Action<double> onChange, Action? onRelease = null)
    {
        var slider = new Slider { Minimum = min, Maximum = max, Value = value, HorizontalAlignment = HorizontalAlignment.Stretch };
        slider.ValueChanged += (_, e) => onChange(e.NewValue);
        if (onRelease is not null)
            slider.AddHandler(InputElement.PointerCaptureLostEvent, (_, _) => onRelease(), handledEventsToo: true);
        return slider;
    }

    /// <summary>A new solid brush for a "#RRGGBB" colour. Allocates; cache it in per-frame code.</summary>
    public static IBrush Brush(string hex)
    {
        var c = Rgb.FromHex(hex);
        return new SolidColorBrush(Color.FromRgb(Rgb.ToByte(c.R), Rgb.ToByte(c.G), Rgb.ToByte(c.B)));
    }

    /// <summary>A "#RRGGBB" colour with the given alpha (0–255).</summary>
    public static Color ToColor(string hex, byte alpha = 255)
    {
        var c = Rgb.FromHex(hex);
        return Color.FromArgb(alpha, Rgb.ToByte(c.R), Rgb.ToByte(c.G), Rgb.ToByte(c.B));
    }

    /// <summary>
    /// A colour swatch that opens a flyout with hue/saturation/brightness sliders, a hex box and presets.
    /// <paramref name="onPreview"/> fires while dragging; <paramref name="onCommit"/> when the flyout closes.
    /// </summary>
    /// <param name="hex">The initial "#RRGGBB" colour.</param>
    /// <param name="onPreview">Gets each new colour as "#RRGGBB" (upper case).</param>
    /// <param name="onCommit">Runs when the flyout closes, changed or not.</param>
    public static Button ColorPicker(string hex, Action<string> onPreview, Action onCommit) => new HexColorPicker(hex, onPreview, onCommit).Button;
}
