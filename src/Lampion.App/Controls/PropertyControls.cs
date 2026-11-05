using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Lampion.App.Controls;

/// <summary>
/// After Effects' "hot text": a blue value you drag sideways to change, or click to type into.
/// Shift drags ten times faster, Ctrl ten times finer.
/// </summary>
public sealed class ScrubValue : Panel
{
    private static readonly IBrush ValueBrush = new SolidColorBrush(Color.FromRgb(0x5C, 0xA8, 0xFF));
    private readonly TextBlock _text = new() { Foreground = ValueBrush, VerticalAlignment = VerticalAlignment.Center, Cursor = new Cursor(StandardCursorType.SizeWestEast) };
    private readonly TextBox _box = new() { IsVisible = false, MinHeight = 20, Padding = new Thickness(3, 0), MinWidth = 70 };
    private readonly double _min;
    private readonly double _max;
    private readonly double _step;
    private readonly double _scale;
    private readonly string _format;
    private readonly string _unit;
    private double _value;
    private Point? _pressAt;
    private double _pressValue;
    private bool _dragged;

    /// <param name="scale">Display units per stored unit, e.g. 100 for a 0..1 value shown as %.</param>
    /// <param name="step">Change per pixel dragged, in stored units.</param>
    public ScrubValue(double value, double min, double max, double step, double scale = 1, string format = "0.##", string unit = "")
    {
        _value = value;
        _min = min;
        _max = max;
        _step = step;
        _scale = scale;
        _format = format;
        _unit = unit;
        Children.Add(_text);
        Children.Add(_box);
        Background = Brushes.Transparent;
        HorizontalAlignment = HorizontalAlignment.Left;
        UpdateText();

        _text.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;
            _pressAt = e.GetPosition(this);
            _pressValue = _value;
            _dragged = false;
            e.Pointer.Capture(_text);
            e.Handled = true;
        };
        _text.PointerMoved += (_, e) =>
        {
            if (_pressAt is not { } start)
                return;
            var dx = e.GetPosition(this).X - start.X;
            if (!_dragged && Math.Abs(dx) < 3)
                return;
            _dragged = true;
            var speed = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 10 : e.KeyModifiers.HasFlag(KeyModifiers.Control) ? 0.1 : 1;
            SetValue(_pressValue + dx * _step * speed, live: true);
        };
        _text.PointerReleased += (_, e) =>
        {
            e.Pointer.Capture(null);
            if (_pressAt is null)
                return;
            _pressAt = null;
            if (_dragged)
                Committed?.Invoke(_value);
            else
                BeginTyping();
        };
        _box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
                EndTyping(accept: true);
            else if (e.Key == Key.Escape)
                EndTyping(accept: false);
        };
        _box.LostFocus += (_, _) => EndTyping(accept: true);
    }

    public double Value => _value;

    /// <summary>Fires on every pixel of a drag; nothing is committed yet.</summary>
    public event Action<double>? Changing;

    /// <summary>Fires once when a drag ends or a typed value is accepted.</summary>
    public event Action<double>? Committed;

    public void Show(double value)
    {
        _value = value;
        UpdateText();
    }

    private void SetValue(double value, bool live)
    {
        _value = Math.Clamp(value, _min, _max);
        UpdateText();
        if (live)
            Changing?.Invoke(_value);
    }

    private void UpdateText() => _text.Text = (_value * _scale).ToString(_format, CultureInfo.InvariantCulture) + _unit;

    private void BeginTyping()
    {
        _box.Text = (_value * _scale).ToString(_format, CultureInfo.InvariantCulture);
        _text.IsVisible = false;
        _box.IsVisible = true;
        _box.Focus();
        _box.SelectAll();
    }

    private void EndTyping(bool accept)
    {
        if (!_box.IsVisible)
            return;
        _box.IsVisible = false;
        _text.IsVisible = true;
        if (!accept)
            return;
        var text = new string((_box.Text ?? "").Where(c => char.IsDigit(c) || c is '.' or ',' or '-' or '+').ToArray()).Replace(',', '.');
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var typed))
        {
            SetValue(typed / _scale, live: false);
            Committed?.Invoke(_value);
        }
    }
}

/// <summary>A twirl-down group, like the sections in After Effects' Effect Controls.</summary>
public sealed class TwirlSection : StackPanel
{
    // Open/closed state survives the panel being rebuilt on every selection change.
    private static readonly Dictionary<string, bool> Remembered = [];

    public TwirlSection(string key, Control header, bool openByDefault = true, params Control[] trailing)
    {
        var open = Remembered.GetValueOrDefault(key, openByDefault);
        Body = new StackPanel { Spacing = 1, Margin = new Thickness(16, 2, 0, 6), IsVisible = open };
        var arrow = new TextBlock { Text = open ? "▾" : "▸", Width = 14, Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center };
        var bar = new DockPanel { Background = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2E)), Height = 24, Cursor = new Cursor(StandardCursorType.Hand) };
        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Margin = new Thickness(4, 0), Children = { arrow, header } };
        header.VerticalAlignment = VerticalAlignment.Center;
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Margin = new Thickness(0, 0, 4, 0) };
        foreach (var control in trailing)
        {
            control.VerticalAlignment = VerticalAlignment.Center;
            right.Children.Add(control);
        }
        DockPanel.SetDock(right, Dock.Right);
        bar.Children.Add(right);
        bar.Children.Add(left);
        left.PointerPressed += (_, e) =>
        {
            open = !Body.IsVisible;
            Body.IsVisible = open;
            arrow.Text = open ? "▾" : "▸";
            Remembered[key] = open;
            e.Handled = true;
        };
        Children.Add(bar);
        Children.Add(Body);
        Margin = new Thickness(0, 0, 0, 2);
    }

    public StackPanel Body { get; }

    public static TwirlSection Titled(string key, string title, bool openByDefault = true, params Control[] trailing) =>
        new(key, new TextBlock { Text = title, FontWeight = FontWeight.SemiBold }, openByDefault, trailing);
}

/// <summary>One property line: [stopwatch] name ........ value [◀ ◆ ▶].</summary>
public sealed class PropertyRow : Grid
{
    public PropertyRow(string name, Control value, Control? stopwatch = null, Control? navigator = null, string? tooltip = null)
    {
        ColumnDefinitions = new ColumnDefinitions("20,118,*,Auto");
        MinHeight = 24;
        if (stopwatch is not null)
            Add(stopwatch, 0);
        var label = new TextBlock { Text = name, Foreground = new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xC8)), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        if (tooltip is not null)
            ToolTip.SetTip(label, tooltip);
        Add(label, 1);
        value.VerticalAlignment = VerticalAlignment.Center;
        Add(value, 2);
        if (navigator is not null)
            Add(navigator, 3);
    }

    private void Add(Control control, int column)
    {
        SetColumn(control, column);
        Children.Add(control);
    }
}

/// <summary>Small borderless glyph button used for stopwatches, keyframe arrows and section actions.</summary>
public sealed class GlyphButton : Button
{
    protected override Type StyleKeyOverride => typeof(Button);

    public GlyphButton(string glyph, string tip, Action onClick, bool active = false)
    {
        Content = glyph;
        Padding = new Thickness(3, 0);
        MinHeight = 20;
        MinWidth = 18;
        FontSize = 12;
        Background = Brushes.Transparent;
        BorderThickness = new Thickness(0);
        Foreground = active ? new SolidColorBrush(Color.FromRgb(0x5C, 0xA8, 0xFF)) : Brushes.Gray;
        ToolTip.SetTip(this, tip);
        Click += (_, e) =>
        {
            e.Handled = true;
            onClick();
        };
    }
}
