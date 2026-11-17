using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Lampion.App.Controls.Icons;

/// <summary>Wrapping text with a <see cref="Lucide"/> icon in front of it, e.g. a warning triangle; the icon can be hidden.</summary>
internal sealed class IconText : Grid
{
    private readonly LucideIcon _icon;

    /// <summary>A paragraph showing <paramref name="text"/> after <paramref name="icon"/>, which shows while <paramref name="showIcon"/>.</summary>
    public IconText(Geometry icon, string? text = null, bool showIcon = true, double fontSize = 11)
    {
        ColumnDefinitions = new ColumnDefinitions("Auto,*");
        _icon = new LucideIcon(icon, fontSize + 1) { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 5, 0) };
        Text = new TextBlock { FontSize = fontSize, TextWrapping = TextWrapping.Wrap };
        SetColumn(Text, 1);
        Children.Add(_icon);
        Children.Add(Text);
        Show(text, showIcon);
    }

    /// <summary>The text block, e.g. for its foreground.</summary>
    public TextBlock Text { get; }

    /// <summary>Colours the text and the icon.</summary>
    public IBrush? Foreground
    {
        get => Text.Foreground;
        set
        {
            Text.Foreground = value;
            _icon.Foreground = value;
        }
    }

    /// <summary>Shows <paramref name="text"/>, with the icon when <paramref name="showIcon"/>.</summary>
    public void Show(string? text, bool showIcon)
    {
        Text.Text = text;
        _icon.IsVisible = showIcon;
    }
}
