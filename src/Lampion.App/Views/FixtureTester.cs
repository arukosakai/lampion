using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lampion.App.Controls;
using Lampion.Core.Color;
using Lampion.Core.Engine;
using Lampion.Core.Model;

namespace Lampion.App.Views;

/// <summary>
/// Console-style controls for poking a fixture by function rather than by channel: intensity, colour,
/// strobe and a pan/tilt pad. They write into the shared test values, which the per-channel sliders also edit.
/// </summary>
internal sealed class FixtureTester : StackPanel
{
    private static readonly IBrush Muted = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A));

    private readonly FixtureProfile _profile;
    private readonly List<byte> _values;
    private readonly Action _changed;
    private readonly Slider _intensity;
    private readonly Slider _hue;
    private readonly Slider _saturation;
    private readonly Control _strobeRow;
    private readonly Slider _strobe;
    private readonly PanTiltPad _pad = new() { Width = 170, Height = 132 };
    private readonly Control _padColumn;
    private bool _syncing;

    /// <param name="values">Test values, one per channel of <paramref name="profile"/>; edited in place.</param>
    /// <param name="changed">Called after the tester writes, so the owner can redraw what shows the values.</param>
    public FixtureTester(FixtureProfile profile, List<byte> values, Action changed)
    {
        _profile = profile;
        _values = values;
        _changed = changed;
        Spacing = 8;

        _intensity = Fader(100, 0, 100, "%");
        _hue = Fader(0, 0, 360, "°");
        _saturation = Fader(0, 0, 100, "%");
        _strobe = Fader(0, 0, 100, "%");
        PaintHueTrack(_hue);
        _intensity.ValueChanged += (_, _) => ApplyColour();
        _hue.ValueChanged += (_, _) => ApplyColour();
        _saturation.ValueChanged += (_, _) => ApplyColour();
        _strobe.ValueChanged += (_, e) =>
        {
            if (!_syncing)
                Write(ChannelRole.Strobe, (byte)Math.Round(e.NewValue / 100 * 255));
        };
        _pad.Changed += SetPanTilt;

        var swatches = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        foreach (var (hex, name) in new[] { ("#FFFFFF", "White"), ("#FF0000", "Red"), ("#FF8000", "Orange"), ("#FFE000", "Yellow"), ("#00FF00", "Green"), ("#00FFFF", "Cyan"), ("#0000FF", "Blue"), ("#FF00FF", "Magenta") })
        {
            var color = Ui.ToColor(hex);
            var swatch = new Button { Width = 20, Height = 20, MinHeight = 20, Padding = new Thickness(0), Background = new SolidColorBrush(color), CornerRadius = new CornerRadius(10) };
            ToolTip.SetTip(swatch, name);
            swatch.Click += (_, _) =>
            {
                var (h, s, _) = Rgb.FromHex(hex).ToHsv();
                _syncing = true;
                (_hue.Value, _saturation.Value) = (h * 360, s * 100);
                _syncing = false;
                ApplyColour();
            };
            swatches.Children.Add(swatch);
        }

        _strobeRow = Row("Strobe", _strobe);
        var faders = new StackPanel
        {
            Spacing = 6,
            Children = { Row("Intensity", _intensity), Row("Hue", _hue), Row("Saturation", _saturation), _strobeRow, swatches },
        };
        _padColumn = new StackPanel
        {
            Spacing = 3,
            Children = { new TextBlock { Text = "Pan / tilt", Foreground = Muted, FontSize = 11 }, _pad },
        };
        ToolTip.SetTip(_pad, "Drag to move the head; double-click to centre it");

        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 14 };
        top.Children.Add(faders);
        Grid.SetColumn(_padColumn, 1);
        top.Children.Add(_padColumn);
        Children.Add(top);
    }

    /// <summary>Points the head; also what aiming in the 3D preview calls.</summary>
    public void SetPanTilt(double pan, double tilt)
    {
        WriteWide(ChannelRole.Pan, ChannelRole.PanFine, pan);
        WriteWide(ChannelRole.Tilt, ChannelRole.TiltFine, tilt);
        _changed();
    }

    /// <summary>Follow the profile and the values after something else changed them.</summary>
    public void Sync()
    {
        _padColumn.IsVisible = _profile.HasPanTilt;
        _strobeRow.IsVisible = _profile.Has(ChannelRole.Strobe);
        _pad.PanRange = _profile.PanRange;
        _pad.TiltRange = _profile.TiltRange;
        var emitted = DmxMapper.Decode(new Fixture(), _profile, _values.ToArray());
        _pad.Set(emitted.Pan, emitted.Tilt);
        _syncing = true;
        var strobe = _profile.IndexOf(ChannelRole.Strobe);
        if (strobe >= 0 && strobe < _values.Count)
            _strobe.Value = _values[strobe] / 255.0 * 100;
        _syncing = false;
    }

    private void ApplyColour()
    {
        if (_syncing)
            return;
        var level = _intensity.Value / 100;
        var colour = Rgb.FromHsv(_hue.Value / 360, _saturation.Value / 100, 1);
        var hasDimmer = _profile.Has(ChannelRole.Dimmer);
        // With a dimmer the colour stays at full mix and the dimmer sets the level, as a console does.
        var scale = hasDimmer ? 1 : level;
        var (r, g, b) = (colour.R, colour.G, colour.B);
        var w = 0.0;
        if (_profile.Has(ChannelRole.White) && _profile.HasColor)
        {
            w = Math.Min(r, Math.Min(g, b));
            (r, g, b) = (r - w, g - w, b - w);
        }
        else if (!_profile.HasColor)
            w = 1;
        WriteAll(ChannelRole.Dimmer, level);
        WriteAll(ChannelRole.Red, r * scale);
        WriteAll(ChannelRole.Green, g * scale);
        WriteAll(ChannelRole.Blue, b * scale);
        WriteAll(ChannelRole.White, w * scale);
        _changed();
    }

    private void Write(ChannelRole role, byte value)
    {
        for (var i = 0; i < _profile.Channels.Count && i < _values.Count; i++)
            if (_profile.Channels[i].Role == role)
                _values[i] = value;
        _changed();
    }

    private void WriteAll(ChannelRole role, double level)
    {
        for (var i = 0; i < _profile.Channels.Count && i < _values.Count; i++)
            if (_profile.Channels[i].Role == role)
                _values[i] = Rgb.ToByte(level);
    }

    private void WriteWide(ChannelRole coarse, ChannelRole fine, double value)
    {
        var wide = (int)Math.Round(Math.Clamp(value, 0, 1) * 65535);
        var hasFine = _profile.Has(fine);
        for (var i = 0; i < _profile.Channels.Count && i < _values.Count; i++)
        {
            if (_profile.Channels[i].Role == coarse)
                _values[i] = hasFine ? (byte)(wide >> 8) : (byte)Math.Round(value * 255);
            else if (_profile.Channels[i].Role == fine)
                _values[i] = (byte)(wide & 0xFF);
        }
    }

    private static Slider Fader(double value, double min, double max, string unit)
    {
        var slider = new Slider { Minimum = min, Maximum = max, Value = value, VerticalAlignment = VerticalAlignment.Center };
        slider.Tag = unit;
        return slider;
    }

    private static Control Row(string label, Slider slider)
    {
        var unit = slider.Tag as string ?? "";
        var readout = new TextBlock { Text = $"{slider.Value:0}{unit}", Width = 40, TextAlignment = TextAlignment.Right, Foreground = Muted, VerticalAlignment = VerticalAlignment.Center };
        slider.ValueChanged += (_, e) => readout.Text = $"{e.NewValue:0}{unit}";
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("72,*,Auto"), Height = 28 };
        grid.Children.Add(new TextBlock { Text = label, Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(slider, 1);
        grid.Children.Add(slider);
        Grid.SetColumn(readout, 2);
        grid.Children.Add(readout);
        return grid;
    }

    private static void PaintHueTrack(Slider slider)
    {
        var rainbow = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative) };
        for (var i = 0; i <= 6; i++)
        {
            var c = Rgb.FromHsv(i / 6.0, 1, 1);
            rainbow.GradientStops.Add(new GradientStop(Color.FromRgb(Rgb.ToByte(c.R), Rgb.ToByte(c.G), Rgb.ToByte(c.B)), i / 6.0));
        }
        foreach (var key in new[] { "SliderTrackFill", "SliderTrackFillPointerOver", "SliderTrackFillPressed" })
            slider.Resources[key] = rainbow;
        foreach (var key in new[] { "SliderTrackValueFill", "SliderTrackValueFillPointerOver", "SliderTrackValueFillPressed" })
            slider.Resources[key] = Brushes.Transparent;
        foreach (var key in new[] { "SliderThumbBackground", "SliderThumbBackgroundPointerOver", "SliderThumbBackgroundPressed" })
            slider.Resources[key] = Brushes.White;
    }
}
