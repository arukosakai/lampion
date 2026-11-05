using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Lampion.App.Controls;

/// <summary>
/// After Effects' "hot text": a blue value you drag sideways to change, or click to type into.
/// Shift drags ten times faster, Ctrl ten times finer. Dragging only allocates when the shown text changes.
/// </summary>
internal sealed class ScrubValue : Panel
{
    private const double DragThreshold = 3;

    private static readonly IBrush ValueBrush = new SolidColorBrush(Color.FromRgb(0x5C, 0xA8, 0xFF));
    private static readonly Cursor ScrubCursor = new(StandardCursorType.SizeWestEast);

    private readonly TextBlock _text = new() { Foreground = ValueBrush, VerticalAlignment = VerticalAlignment.Center, Cursor = ScrubCursor };
    private readonly TextBox _box = new() { IsVisible = false, MinHeight = 20, Padding = new Thickness(3, 0), MinWidth = 70 };
    private readonly ScrubRange _range;
    private double _value;
    private Point? _pressAt;
    private double _pressValue;
    private bool _dragged;

    /// <summary>Creates a scrubbable value; <paramref name="value"/> is shown as given, not clamped.</summary>
    /// <param name="value">Initial value, in stored units.</param>
    /// <param name="min">Smallest value a drag or typed entry can set.</param>
    /// <param name="max">Largest value a drag or typed entry can set.</param>
    /// <param name="step">Change per pixel dragged, in stored units.</param>
    /// <param name="scale">Display units per stored unit, e.g. 100 for a 0..1 value shown as %.</param>
    /// <param name="format">.NET number format for the displayed value, invariant culture.</param>
    /// <param name="unit">Suffix shown after the number.</param>
    public ScrubValue(double value, double min, double max, double step, double scale = 1, string format = "0.##", string unit = "")
        : this(value, new ScrubRange(min, max, step, scale, format, unit)) { }

    /// <summary>Creates a scrubbable value; <paramref name="value"/> is shown as given, not clamped.</summary>
    /// <param name="value">Initial value, in stored units.</param>
    /// <param name="range">Limits, drag speed and display format.</param>
    public ScrubValue(double value, ScrubRange range)
    {
        _value = value;
        _range = range;
        Children.Add(_text);
        Children.Add(_box);
        Background = Brushes.Transparent;
        HorizontalAlignment = HorizontalAlignment.Left;
        UpdateText();

        _text.PointerPressed += OnPressed;
        _text.PointerMoved += OnMoved;
        _text.PointerReleased += OnReleased;
        _box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
                EndTyping(accept: true);
            else if (e.Key == Key.Escape)
                EndTyping(accept: false);
        };
        _box.LostFocus += (_, _) => EndTyping(accept: true);
    }

    /// <summary>The current value in stored units.</summary>
    public double Value => _value;

    /// <summary>Fires on every pixel of a drag; nothing is committed yet.</summary>
    public event Action<double>? Changing;

    /// <summary>Fires once when a drag ends or a typed value is accepted.</summary>
    public event Action<double>? Committed;

    /// <summary>Displays <paramref name="value"/> without raising any event, e.g. as the playhead moves. Cheap when unchanged.</summary>
    public void Show(double value)
    {
        if (_value.Equals(value))
            return;
        _value = value;
        UpdateText();
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        _pressAt = e.GetPosition(this);
        _pressValue = _value;
        _dragged = false;
        e.Pointer.Capture(_text);
        e.Handled = true;
    }

    private void OnMoved(object? sender, PointerEventArgs e)
    {
        if (_pressAt is not { } start)
            return;
        var dx = e.GetPosition(this).X - start.X;
        if (!_dragged && Math.Abs(dx) < DragThreshold)
            return;
        _dragged = true;
        var modifiers = e.KeyModifiers;
        var speed = (modifiers & KeyModifiers.Shift) != 0 ? 10 : (modifiers & KeyModifiers.Control) != 0 ? 0.1 : 1;
        SetValue(_pressValue + dx * _range.Step * speed, live: true);
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        e.Pointer.Capture(null);
        if (_pressAt is null)
            return;
        _pressAt = null;
        if (_dragged)
            Committed?.Invoke(_value);
        else
            BeginTyping();
    }

    private void SetValue(double value, bool live)
    {
        _value = Math.Clamp(value, _range.Min, _range.Max);
        UpdateText();
        if (live)
            Changing?.Invoke(_value);
    }

    private void UpdateText()
    {
        var shown = _value * _range.Scale;
        var unit = _range.Unit;
        Span<char> buffer = stackalloc char[64];
        if (shown.TryFormat(buffer, out var written, _range.Format, CultureInfo.InvariantCulture) && written + unit.Length <= buffer.Length)
        {
            unit.AsSpan().CopyTo(buffer[written..]);
            var text = buffer[..(written + unit.Length)];
            if (_text.Text is not { } current || !current.AsSpan().SequenceEqual(text))
                _text.Text = new string(text);
            return;
        }
        _text.Text = shown.ToString(_range.Format, CultureInfo.InvariantCulture) + unit;
    }

    private void BeginTyping()
    {
        _box.Text = (_value * _range.Scale).ToString(_range.Format, CultureInfo.InvariantCulture);
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
        // Units and stray characters are dropped, and a decimal comma counts as a point.
        var text = new string((_box.Text ?? "").Where(c => char.IsDigit(c) || c is '.' or ',' or '-' or '+').ToArray()).Replace(',', '.');
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var typed))
        {
            SetValue(typed / _range.Scale, live: false);
            Committed?.Invoke(_value);
        }
    }
}
