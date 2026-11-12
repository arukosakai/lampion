using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lampion.App.Views.Live;

/// <summary>The shared look of widget panels: the same dark rounded card as the fader strips.</summary>
internal static class WidgetFrame
{
    private static readonly IBrush Card = new SolidColorBrush(Color.FromRgb(0x24, 0x26, 0x2B));

    /// <summary>Styles <paramref name="border"/> as a widget card with <paramref name="menu"/> on right-click.</summary>
    public static void Apply(Border border, ContextMenu menu)
    {
        border.Background = Card;
        border.CornerRadius = new CornerRadius(6);
        border.Padding = new Thickness(8);
        border.Margin = new Thickness(0, 0, 0, 8);
        border.ClipToBounds = true;
        border.ContextMenu = menu;
    }
}
