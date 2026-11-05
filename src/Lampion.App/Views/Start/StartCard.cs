using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Lampion.App.Views.Start;

/// <summary>The start screen's building blocks: clickable cards and section headings.</summary>
internal static class StartCard
{
    /// <summary>Width of a template card, in pixels.</summary>
    public const double TileWidth = 210;

    /// <summary>Height of a template card, in pixels.</summary>
    public const double TileHeight = 132;

    /// <summary>The highlight colour of the start screen (badges of the user's own templates).</summary>
    public static readonly IBrush Accent = new SolidColorBrush(Color.FromRgb(0x2D, 0x8C, 0xEB));

    private static readonly IBrush Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2D));
    private static readonly IBrush Hover = new SolidColorBrush(Color.FromRgb(0x35, 0x37, 0x3D));

    /// <summary>A rounded card around <paramref name="content"/> that lights up on hover and runs <paramref name="onClick"/> on a left click.</summary>
    public static Border Create(Control content, Action onClick)
    {
        var card = new Border
        {
            Background = Background,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = content,
        };
        card.PointerEntered += (_, _) => card.Background = Hover;
        card.PointerExited += (_, _) => card.Background = Background;
        card.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Left)
                onClick();
        };
        return card;
    }

    /// <summary>A template-sized card, laid out for the gallery.</summary>
    public static Border Tile(Control content, Action onClick)
    {
        var card = Create(content, onClick);
        card.Width = TileWidth;
        card.Height = TileHeight;
        card.Margin = new Thickness(0, 0, 10, 10);
        return card;
    }

    /// <summary>A sidebar action: an icon, a title and a line of explanation.</summary>
    public static Border Action(string icon, string title, string subtitle, Action onClick) =>
        Create(
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = icon, FontSize = 20, Width = 26, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center },
                    new StackPanel
                    {
                        Children =
                        {
                            new TextBlock { Text = title, FontWeight = FontWeight.SemiBold, FontSize = 13 },
                            new TextBlock { Text = subtitle, Foreground = Brushes.Gray, FontSize = 11, TextWrapping = TextWrapping.Wrap, MaxWidth = 200 },
                        },
                    },
                },
            },
            onClick
        );

    /// <summary>A section heading, with <paramref name="top"/> pixels of space above it.</summary>
    public static TextBlock Heading(string text, double top = 0) =>
        new() { Text = text, FontSize = 16, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, top, 0, 2) };
}
