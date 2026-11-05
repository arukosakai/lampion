using Avalonia.Controls;
using Lampion.App.Controls;
using Lampion.Core.Color;
using Lampion.Core.Engine;
using Lampion.Core.Model.Rig;

namespace Lampion.App.Views.ProfileEditor;

/// <summary>
/// Console-style controls for poking a fixture by function rather than by channel: intensity, colour,
/// strobe and a pan/tilt pad. They write into the shared test values, which the per-channel sliders also
/// edit. Writing and <see cref="Sync"/> allocate nothing, so dragging stays cheap.
/// </summary>
internal sealed class FixtureTester : StackPanel
{
    // Decode only reads the gel colour, which the default fixture supplies.
    private static readonly Fixture Probe = new();

    private readonly FixtureProfile _profile;
    private readonly ProfileTestValues _values;
    private readonly Action _changed;
    private readonly TesterFader _intensity = new("Intensity", 100, 100, NumberLabels.Percent);
    private readonly TesterFader _hue = new("Hue", 0, 360, NumberLabels.Degrees);
    private readonly TesterFader _saturation = new("Saturation", 0, 100, NumberLabels.Percent);
    private readonly TesterFader _strobe = new("Strobe", 0, 100, NumberLabels.Percent);
    private readonly PanTiltPad _pad = new() { Width = 170, Height = 132 };
    private readonly Control _padColumn;
    private bool _syncing;

    /// <param name="profile">The profile being tested; its channels say which values each control writes.</param>
    /// <param name="values">Test values, one per channel of <paramref name="profile"/>; edited in place.</param>
    /// <param name="changed">Called after the tester writes, so the owner can redraw what shows the values.</param>
    public FixtureTester(FixtureProfile profile, ProfileTestValues values, Action changed)
    {
        _profile = profile;
        _values = values;
        _changed = changed;
        Spacing = 8;

        _hue.PaintHueTrack();
        _intensity.Slider.ValueChanged += (_, _) => ApplyColour();
        _hue.Slider.ValueChanged += (_, _) => ApplyColour();
        _saturation.Slider.ValueChanged += (_, _) => ApplyColour();
        _strobe.Slider.ValueChanged += (_, e) =>
        {
            if (!_syncing)
                Write(ChannelRole.Strobe, (byte)Math.Round(e.NewValue / 100 * 255));
        };
        _pad.Changed += SetPanTilt;
        ToolTip.SetTip(_pad, "Drag to move the head; double-click to centre it");

        var swatches = new ColourSwatches();
        swatches.Picked += PickColour;
        var faders = new StackPanel
        {
            Spacing = 6,
            Children = { _intensity, _hue, _saturation, _strobe, swatches },
        };
        _padColumn = new StackPanel
        {
            Spacing = 3,
            Children = { new TextBlock { Text = "Pan / tilt", Foreground = ProfileEditorUi.Muted, FontSize = 11 }, _pad },
        };

        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 14 };
        top.Children.Add(faders);
        Grid.SetColumn(_padColumn, 1);
        top.Children.Add(_padColumn);
        Children.Add(top);
    }

    /// <summary>Points the head; also what aiming in the 3D preview calls. Pan and tilt are 0..1, 0.5 centred.</summary>
    public void SetPanTilt(double pan, double tilt)
    {
        var values = _values.Span;
        TestValueWriter.WriteWide(_profile, values, ChannelRole.Pan, ChannelRole.PanFine, pan);
        TestValueWriter.WriteWide(_profile, values, ChannelRole.Tilt, ChannelRole.TiltFine, tilt);
        _changed();
    }

    /// <summary>Follows the profile and the values after something else changed them.</summary>
    public void Sync()
    {
        _padColumn.IsVisible = _profile.HasPanTilt;
        _strobe.IsVisible = _profile.Has(ChannelRole.Strobe);
        _pad.PanRange = _profile.PanRange;
        _pad.TiltRange = _profile.TiltRange;
        var emitted = DmxMapper.Decode(Probe, _profile, _values.Span);
        _pad.Set(emitted.Pan, emitted.Tilt);
        _syncing = true;
        var strobe = _profile.IndexOf(ChannelRole.Strobe);
        if (strobe >= 0 && strobe < _values.Count)
            _strobe.Slider.Value = _values[strobe] / 255.0 * 100;
        _syncing = false;
    }

    private void PickColour(double hue, double saturation)
    {
        _syncing = true;
        (_hue.Slider.Value, _saturation.Slider.Value) = (hue, saturation);
        _syncing = false;
        ApplyColour();
    }

    private void ApplyColour()
    {
        if (_syncing)
            return;
        var level = _intensity.Slider.Value / 100;
        var colour = Rgb.FromHsv(_hue.Slider.Value / 360, _saturation.Slider.Value / 100, 1);
        // With a dimmer the colour stays at full mix and the dimmer sets the level, as a console does.
        var scale = _profile.Has(ChannelRole.Dimmer) ? 1 : level;
        var (r, g, b) = (colour.R, colour.G, colour.B);
        var w = 0.0;
        if (_profile.Has(ChannelRole.White) && _profile.HasColor)
        {
            w = Math.Min(r, Math.Min(g, b));
            (r, g, b) = (r - w, g - w, b - w);
        }
        else if (!_profile.HasColor)
            w = 1;
        var values = _values.Span;
        TestValueWriter.WriteLevel(_profile, values, ChannelRole.Dimmer, level);
        TestValueWriter.WriteLevel(_profile, values, ChannelRole.Red, r * scale);
        TestValueWriter.WriteLevel(_profile, values, ChannelRole.Green, g * scale);
        TestValueWriter.WriteLevel(_profile, values, ChannelRole.Blue, b * scale);
        TestValueWriter.WriteLevel(_profile, values, ChannelRole.White, w * scale);
        _changed();
    }

    private void Write(ChannelRole role, byte value)
    {
        TestValueWriter.Write(_profile, _values.Span, role, value);
        _changed();
    }
}
