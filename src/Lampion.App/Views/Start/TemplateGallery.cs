using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lampion.App.Editor;
using Lampion.Core.Persistence;

namespace Lampion.App.Views.Start;

/// <summary>The start screen's template tiles: built-in venues, the user's saved layouts and "any layout file".</summary>
internal sealed class TemplateGallery : WrapPanel
{
    private const int MaxSwatches = 8;
    private static readonly IBrush BuiltInBadge = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x40));

    private readonly StartActions _actions;

    /// <summary>The templates as they are on disk now; clicking one starts a show through <paramref name="actions"/>.</summary>
    public TemplateGallery(StartActions actions)
    {
        _actions = actions;
        Fill();
    }

    /// <summary>Rebuilds the tiles from the built-in and saved templates.</summary>
    public void Fill()
    {
        Children.Clear();
        foreach (var (layout, description) in StageTemplates.BuiltIn())
            Children.Add(Tile(layout, description, "Built-in", null));
        foreach (var (path, layout) in UserData.Templates())
            Children.Add(Tile(layout, $"{layout.Stage.Objects.Count} set pieces", "Yours", path));
        Children.Add(BrowseTile());
        Children.Add(
            new TextBlock
            {
                Text = "Save your own from a show with Stage → Save layout as template…",
                Foreground = Brushes.DimGray,
                FontSize = 11,
                Width = 420,
                Margin = new Thickness(0, 4, 0, 0),
            }
        );
    }

    private Border Tile(StageLayout layout, string description, string badge, string? userPath)
    {
        var top = new DockPanel();
        var tag = new Border
        {
            Background = userPath is null ? BuiltInBadge : StartCard.Accent,
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5, 1),
            Child = new TextBlock { Text = badge, FontSize = 10 },
            VerticalAlignment = VerticalAlignment.Top,
        };
        DockPanel.SetDock(tag, Dock.Right);
        top.Children.Add(tag);
        top.Children.Add(new TextBlock { Text = layout.Name, FontWeight = FontWeight.SemiBold, FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis });

        var body = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                top,
                new TextBlock { Text = description, Foreground = Brushes.Gray, FontSize = 11, TextWrapping = TextWrapping.Wrap, Height = 44 },
                new TextBlock { Text = $"💡 {layout.Fixtures.Count} fixtures · {layout.Groups.Count} groups", FontSize = 11 },
                Swatches(layout),
            },
        };
        var card = StartCard.Tile(body, () => _actions.NewFromLayout(layout));
        if (userPath is not null)
        {
            var delete = new MenuItem { Header = "Delete template" };
            delete.Click += (_, _) => Delete(userPath);
            card.ContextMenu = new ContextMenu { Items = { delete } };
        }
        return card;
    }

    private Border BrowseTile() =>
        StartCard.Tile(
            new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    new TextBlock { Text = "📐", FontSize = 26, HorizontalAlignment = HorizontalAlignment.Center },
                    new TextBlock { Text = "Layout file…", FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center },
                    new TextBlock { Text = "Any .lampstage", Foreground = Brushes.Gray, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center },
                },
            },
            () => _ = _actions.BrowseLayout()
        );

    private void Delete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _actions.ShowError($"Could not delete {Path.GetFileName(path)}: {ex.Message}");
            return;
        }
        Fill();
    }

    private static StackPanel Swatches(StageLayout layout)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
        foreach (var group in layout.Groups.Take(MaxSwatches))
            row.Children.Add(new Border { Width = 18, Height = 6, CornerRadius = new CornerRadius(3), Background = Ui.Brush(group.Color) });
        return row;
    }
}
