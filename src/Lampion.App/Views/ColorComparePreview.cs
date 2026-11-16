using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Lampion.App.Views;

/// <summary>
/// After Effects' new/original swatch pair: the colour being picked on top, the one the picker opened with
/// underneath. Clicking the original goes back to it.
/// </summary>
internal sealed class ColorComparePreview : StackPanel
{
    private static readonly Cursor HandCursor = new(StandardCursorType.Hand);

    private readonly Border _new;
    private readonly Border _original;
    private string _originalHex = "#FFFFFF";

    /// <summary>A preview whose original swatch calls <paramref name="revert"/> with the original hex when clicked.</summary>
    public ColorComparePreview(Action<string> revert)
    {
        Spacing = 2;
        _new = Swatch(new CornerRadius(3, 3, 0, 0));
        _original = Swatch(new CornerRadius(0, 0, 3, 3));
        _original.Cursor = HandCursor;
        ToolTip.SetTip(_new, "New colour");
        ToolTip.SetTip(_original, "Original colour — click to go back to it");
        _original.PointerPressed += (_, e) =>
        {
            revert(_originalHex);
            e.Handled = true;
        };
        Children.Add(Caption("new"));
        Children.Add(new StackPanel { Children = { _new, _original } });
        Children.Add(Caption("original"));
    }

    /// <summary>Starts a picking session: both swatches show <paramref name="hex"/>.</summary>
    public void Begin(string hex)
    {
        _originalHex = hex;
        _original.Background = Ui.Brush(hex);
        Show(hex);
    }

    /// <summary>Shows <paramref name="hex"/> as the new colour.</summary>
    public void Show(string hex) => _new.Background = Ui.Brush(hex);

    private static Border Swatch(CornerRadius corners) =>
        new()
        {
            Width = 56,
            Height = 32,
            CornerRadius = corners,
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(1),
        };

    private static TextBlock Caption(string text) => new() { Text = text, FontSize = 10, Foreground = Brushes.Gray };
}
