using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Lampion.App.Editor;
using Lampion.Core.Model;
using Lampion.Core.Persistence;

namespace Lampion.App.Views;

public sealed record StartChoice(Project Project, string? Path);

/// <summary>
/// The home screen shown on launch (and from File → New): recent shows, templates, a blank show or the demo.
/// </summary>
public sealed class StartWindow : Window
{
    private static readonly IBrush CardBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2D));
    private static readonly IBrush CardHover = new SolidColorBrush(Color.FromRgb(0x35, 0x37, 0x3D));
    private static readonly IBrush Accent = new SolidColorBrush(Color.FromRgb(0x2D, 0x8C, 0xEB));

    private readonly Action<StartChoice>? _onChosen;
    private readonly StackPanel _recent = new() { Spacing = 6 };
    private readonly WrapPanel _templates = new();
    private readonly TextBlock _error = new() { Foreground = Brushes.OrangeRed, TextWrapping = TextWrapping.Wrap, IsVisible = false };

    public StartWindow(Action<StartChoice>? onChosen = null)
    {
        _onChosen = onChosen;
        Title = "Lampion";
        Width = 1040;
        Height = 660;
        MinWidth = 820;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var root = new Grid { ColumnDefinitions = new ColumnDefinitions("300,*") };
        root.Children.Add(Sidebar());
        var content = new ScrollViewer
        {
            Content = new StackPanel
            {
                Margin = new Thickness(32, 28, 32, 28),
                Spacing = 10,
                Children = { Heading("Recent shows"), _recent, Heading("Start from a template", top: 18), _templates, _error },
            },
        };
        Grid.SetColumn(content, 1);
        root.Children.Add(content);
        Content = root;

        FillRecent();
        FillTemplates();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && _onChosen is null)
                Close();
        };
    }

    /// <summary>What the user picked; null if the window was closed without choosing.</summary>
    public StartChoice? Result { get; private set; }

    private void Choose(Project project, string? path)
    {
        Result = new StartChoice(project, path);
        if (path is not null)
            UserData.AddRecent(path);
        _onChosen?.Invoke(Result);
        Close();
    }

    private void ShowError(string message)
    {
        _error.Text = message;
        _error.IsVisible = true;
    }

    // ---- Sidebar ---------------------------------------------------------------------------------------

    private Control Sidebar()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var actions = new StackPanel { Spacing = 8 };
        actions.Children.Add(Action("✚", "New empty show", "A bare stage, no fixtures yet", () => Choose(ProjectFactory.Empty(), null)));
        if (UserData.DefaultLayout() is { } layout)
            actions.Children.Add(Action("★", "New from your default", $"\"{layout.Name}\" · {layout.Fixtures.Count} fixtures", () => Choose(Named(ProjectFactory.FromLayout(layout)), null)));
        actions.Children.Add(Action("📂", "Open show…", "A .lampion file from disk", () => _ = OpenFile()));
        actions.Children.Add(Action("✨", "Open the demo", "A short show with pars, moving heads and a rainbow", () => Choose(ProjectFactory.Demo(), null)));

        var panel = new DockPanel { Background = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x22)) };
        var header = new StackPanel
        {
            Margin = new Thickness(26, 34, 26, 26),
            Spacing = 2,
            Children =
            {
                new TextBlock { Text = "🏮 Lampion", FontSize = 30, FontWeight = FontWeight.Bold },
                new TextBlock { Text = "Light shows on a timeline", Foreground = Brushes.Gray, FontSize = 13 },
            },
        };
        var footer = new TextBlock
        {
            Text = $"v{version?.ToString(3) ?? "dev"}",
            Foreground = Brushes.DimGray,
            FontSize = 11,
            Margin = new Thickness(26, 0, 26, 18),
        };
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        panel.Children.Add(header);
        panel.Children.Add(footer);
        panel.Children.Add(new Border { Padding = new Thickness(18, 0), Child = actions });
        return panel;
    }

    private static Project Named(Project project)
    {
        project.Name = "Untitled show";
        return project;
    }

    private Control Action(string icon, string title, string subtitle, Action onClick) =>
        Card(
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

    private async Task OpenFile()
    {
        var path = await Dialogs.PickOpen(this, "Open show", ("Lampion show", ["*" + ProjectSerializer.Extension]));
        if (path is not null)
            OpenProject(path);
    }

    private void OpenProject(string path)
    {
        try
        {
            Choose(ProjectSerializer.Load(path), path);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            ShowError($"Could not open {Path.GetFileName(path)}: {ex.Message}");
        }
    }

    // ---- Recent ----------------------------------------------------------------------------------------

    private void FillRecent()
    {
        _recent.Children.Clear();
        var recent = UserData.RecentProjects();
        if (recent.Count == 0)
        {
            _recent.Children.Add(new TextBlock { Text = "Nothing yet — shows you open or save will appear here.", Foreground = Brushes.Gray });
            return;
        }
        foreach (var path in recent.Take(8))
        {
            var exists = File.Exists(path);
            var when = exists ? File.GetLastWriteTime(path).ToString("g") : "file not found";
            var remove = new Button { Content = "✕", Padding = new Thickness(6, 0), MinHeight = 22, VerticalAlignment = VerticalAlignment.Center, Background = Brushes.Transparent };
            ToolTip.SetTip(remove, "Remove from the list");
            remove.Click += (_, e) =>
            {
                e.Handled = true;
                UserData.RemoveRecent(path);
                FillRecent();
            };
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
            var text = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = Path.GetFileNameWithoutExtension(path), FontWeight = FontWeight.SemiBold, Foreground = exists ? Brushes.White : Brushes.Gray },
                    new TextBlock { Text = path, Foreground = Brushes.Gray, FontSize = 11, TextTrimming = TextTrimming.PathSegmentEllipsis },
                },
            };
            var date = new TextBlock { Text = when, Foreground = exists ? Brushes.Gray : Brushes.OrangeRed, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0) };
            Grid.SetColumn(date, 1);
            Grid.SetColumn(remove, 2);
            row.Children.Add(text);
            row.Children.Add(date);
            row.Children.Add(remove);
            _recent.Children.Add(Card(row, () =>
            {
                if (exists)
                    OpenProject(path);
                else
                    ShowError($"{path} no longer exists. Remove it with ✕, or open it from its new location.");
            }));
        }
    }

    // ---- Templates -------------------------------------------------------------------------------------

    private void FillTemplates()
    {
        _templates.Children.Clear();
        foreach (var (layout, description) in StageTemplates.BuiltIn())
            _templates.Children.Add(TemplateCard(layout, description, "Built-in", null));
        foreach (var (path, layout) in UserData.Templates())
            _templates.Children.Add(TemplateCard(layout, $"{layout.Stage.Objects.Count} set pieces", "Yours", path));
        var browse = Card(
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
            () => _ = BrowseLayout()
        );
        browse.Width = 210;
        browse.Height = 132;
        browse.Margin = new Thickness(0, 0, 10, 10);
        _templates.Children.Add(browse);
        _templates.Children.Add(
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

    private Control TemplateCard(StageLayout layout, string description, string badge, string? userPath)
    {
        var top = new DockPanel();
        var tag = new Border
        {
            Background = userPath is null ? new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x40)) : Accent,
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
        var card = Card(body, () => Choose(Named(ProjectFactory.FromLayout(layout)), null));
        card.Width = 210;
        card.Height = 132;
        card.Margin = new Thickness(0, 0, 10, 10);
        if (userPath is not null)
        {
            var delete = new MenuItem { Header = "Delete template" };
            delete.Click += (_, _) =>
            {
                File.Delete(userPath);
                FillTemplates();
            };
            card.ContextMenu = new ContextMenu { Items = { delete } };
        }
        return card;
    }

    private static Control Swatches(StageLayout layout)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
        foreach (var group in layout.Groups.Take(8))
            row.Children.Add(new Border { Width = 18, Height = 6, CornerRadius = new CornerRadius(3), Background = Ui.Brush(group.Color) });
        return row;
    }

    private async Task BrowseLayout()
    {
        var path = await Dialogs.PickOpen(this, "Start from a stage layout", ("Lampion stage layout", ["*" + StageLayout.Extension]));
        if (path is null)
            return;
        if (UserData.TryLoadLayout(path) is { } layout)
            Choose(Named(ProjectFactory.FromLayout(layout)), null);
        else
            ShowError($"{Path.GetFileName(path)} is not a readable stage layout.");
    }

    // ---- Shared bits -----------------------------------------------------------------------------------

    private static TextBlock Heading(string text, double top = 0) =>
        new() { Text = text, FontSize = 16, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, top, 0, 2) };

    private static Border Card(Control content, Action onClick)
    {
        var card = new Border
        {
            Background = CardBrush,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = content,
        };
        card.PointerEntered += (_, _) => card.Background = CardHover;
        card.PointerExited += (_, _) => card.Background = CardBrush;
        card.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Left)
                onClick();
        };
        return card;
    }
}
