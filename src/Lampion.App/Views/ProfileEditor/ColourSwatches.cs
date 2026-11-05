using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lampion.Core.Color;

namespace Lampion.App.Views.ProfileEditor;

/// <summary>A row of round preset-colour buttons (white, red, … magenta) for the tester.</summary>
internal sealed class ColourSwatches : StackPanel
{
    private static readonly (string Hex, string Name)[] Presets =
    [
        ("#FFFFFF", "White"),
        ("#FF0000", "Red"),
        ("#FF8000", "Orange"),
        ("#FFE000", "Yellow"),
        ("#00FF00", "Green"),
        ("#00FFFF", "Cyan"),
        ("#0000FF", "Blue"),
        ("#FF00FF", "Magenta"),
    ];

    /// <summary>Creates the row; listen to <see cref="Picked"/>.</summary>
    public ColourSwatches()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 5;
        foreach (var (hex, name) in Presets)
        {
            var swatch = new Button { Width = 20, Height = 20, MinHeight = 20, Padding = new Thickness(0), Background = new SolidColorBrush(Ui.ToColor(hex)), CornerRadius = new CornerRadius(10) };
            ToolTip.SetTip(swatch, name);
            var (h, s, _) = Rgb.FromHex(hex).ToHsv();
            swatch.Click += (_, _) => Picked?.Invoke(h * 360, s * 100);
            Children.Add(swatch);
        }
    }

    /// <summary>Raised with the clicked colour's hue (0–360°) and saturation (0–100 %).</summary>
    public event Action<double, double>? Picked;
}
