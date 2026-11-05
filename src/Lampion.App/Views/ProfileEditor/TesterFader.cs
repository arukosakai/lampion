using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lampion.Core.Color;

namespace Lampion.App.Views.ProfileEditor;

/// <summary>A labelled tester slider with a live readout in its unit; the readout never formats while dragging.</summary>
internal sealed class TesterFader : Grid
{
    private static readonly string[] TrackKeys = ["SliderTrackFill", "SliderTrackFillPointerOver", "SliderTrackFillPressed"];
    private static readonly string[] ValueFillKeys = ["SliderTrackValueFill", "SliderTrackValueFillPointerOver", "SliderTrackValueFillPressed"];
    private static readonly string[] ThumbKeys = ["SliderThumbBackground", "SliderThumbBackgroundPointerOver", "SliderThumbBackgroundPressed"];

    /// <param name="label">Name on the left, e.g. "Intensity".</param>
    /// <param name="value">Starting value, in the unit of <paramref name="labels"/>.</param>
    /// <param name="max">Slider maximum; the minimum is 0.</param>
    /// <param name="labels">Readouts for the slider's whole values.</param>
    public TesterFader(string label, double value, double max, NumberLabels labels)
    {
        Slider = new Slider { Minimum = 0, Maximum = max, Value = value, VerticalAlignment = VerticalAlignment.Center };
        var readout = new TextBlock { Text = labels.Of(value), Width = 40, TextAlignment = TextAlignment.Right, Foreground = ProfileEditorUi.Muted, VerticalAlignment = VerticalAlignment.Center };
        Slider.ValueChanged += (_, e) => readout.Text = labels.Of(e.NewValue);

        ColumnDefinitions = new ColumnDefinitions("72,*,Auto");
        Height = 28;
        Children.Add(new TextBlock { Text = label, Foreground = ProfileEditorUi.Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
        SetColumn(Slider, 1);
        Children.Add(Slider);
        SetColumn(readout, 2);
        Children.Add(readout);
    }

    /// <summary>The slider, for reading and setting the value and listening to changes.</summary>
    public Slider Slider { get; }

    /// <summary>Paints the track as a hue rainbow with a white thumb, for picking a hue in degrees.</summary>
    public void PaintHueTrack()
    {
        var rainbow = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative) };
        for (var i = 0; i <= 6; i++)
        {
            var c = Rgb.FromHsv(i / 6.0, 1, 1);
            rainbow.GradientStops.Add(new GradientStop(Color.FromRgb(Rgb.ToByte(c.R), Rgb.ToByte(c.G), Rgb.ToByte(c.B)), i / 6.0));
        }
        foreach (var key in TrackKeys)
            Slider.Resources[key] = rainbow;
        foreach (var key in ValueFillKeys)
            Slider.Resources[key] = Brushes.Transparent;
        foreach (var key in ThumbKeys)
            Slider.Resources[key] = Brushes.White;
    }
}
