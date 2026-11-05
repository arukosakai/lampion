using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lampion.Core.Model.Effects;
using LightEffect = Lampion.Core.Model.Effects.Effect;

namespace Lampion.App.Views.EffectsLibrary;

/// <summary>The small colour chip that stands for an effect in the library: its swatch colour, or a gradient of its palette.</summary>
internal static class EffectSwatch
{
    /// <summary>A 14×14 chip for <paramref name="effect"/>; unparseable colours show white.</summary>
    public static Control Create(LightEffect effect)
    {
        var chip = new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(3), Background = Ui.Brush(effect.SwatchColor) };
        IReadOnlyList<string>? stops = effect switch
        {
            RainbowEffect => ["#FF0000", "#00FF00", "#0000FF"],
            GradientEffect g => g.Colors,
            ColorCycleEffect c => c.Colors,
            ColorWaveEffect w => [w.Base, w.Color],
            FireEffect f => [f.Ember, f.Flame],
            _ => null,
        };
        if (stops is { Count: > 1 })
        {
            var gradient = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            };
            for (var i = 0; i < stops.Count; i++)
                gradient.GradientStops.Add(new GradientStop(Color.TryParse(stops[i], out var c) ? c : Colors.White, i / (stops.Count - 1.0)));
            chip.Background = gradient;
        }
        return chip;
    }
}
