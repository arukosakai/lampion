using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Lampion.App.Controls.Icons;

/// <summary>A <see cref="Lucide"/> icon followed by text, e.g. a button's content or a warning line.</summary>
internal sealed class IconLabel : StackPanel
{
    /// <summary>Shows <paramref name="icon"/>, then <paramref name="text"/> when it isn't null or empty.</summary>
    /// <param name="icon">A <see cref="Lucide"/> icon.</param>
    /// <param name="text">The label; null or empty shows the icon alone.</param>
    /// <param name="size">Icon edge in pixels.</param>
    /// <param name="iconAfter">Puts the icon after the text, e.g. a dropdown's chevron.</param>
    public IconLabel(Geometry icon, string? text = null, double size = 14, bool iconAfter = false)
    {
        Orientation = Orientation.Horizontal;
        Spacing = 5;
        Icon = new LucideIcon(icon, size);
        Text = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, IsVisible = !string.IsNullOrEmpty(text) };
        Children.Add(iconAfter ? Text : Icon);
        Children.Add(iconAfter ? Icon : Text);
    }

    /// <summary>The icon, e.g. to swap it or fill it.</summary>
    public LucideIcon Icon { get; }

    /// <summary>The text; hidden while empty.</summary>
    public TextBlock Text { get; }

    /// <summary>Changes the text, hiding it when empty.</summary>
    public void SetText(string? text)
    {
        Text.Text = text;
        Text.IsVisible = !string.IsNullOrEmpty(text);
    }
}
