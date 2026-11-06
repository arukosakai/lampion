using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lampion.App.Editor;

namespace Lampion.App.Views.Start;

/// <summary>The start screen's left column: the app name, the ways to begin a show and the version.</summary>
internal sealed class StartSidebar : DockPanel
{
    /// <summary>A sidebar whose cards run <paramref name="actions"/>.</summary>
    public StartSidebar(StartActions actions)
    {
        Background = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x22));
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
            Text = $"v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "dev"}",
            Foreground = Brushes.DimGray,
            FontSize = 11,
            Margin = new Thickness(26, 0, 26, 18),
        };
        SetDock(header, Dock.Top);
        SetDock(footer, Dock.Bottom);
        Children.Add(header);
        Children.Add(footer);
        Children.Add(new Border { Padding = new Thickness(18, 0), Child = Actions(actions) });
    }

    private static StackPanel Actions(StartActions actions)
    {
        var list = new StackPanel { Spacing = 8 };
        list.Children.Add(StartCard.Action("✚", "New empty show", "A bare stage, no fixtures yet", actions.NewEmpty));
        if (UserData.DefaultLayout() is { } layout)
            list.Children.Add(StartCard.Action("★", "New from your default", $"\"{layout.Name}\" · {layout.Fixtures.Count} fixtures", () => actions.NewFromLayout(layout)));
        list.Children.Add(StartCard.Action("📂", "Open show…", "A .lampion file from disk", () => _ = actions.OpenFile()));
        list.Children.Add(StartCard.Action("🎆", "Open the showcase", "A 90 s festival show: 48 fixtures, truss, pixel towers, every effect", actions.OpenShowcase));
        list.Children.Add(StartCard.Action("✨", "Open the demo", "A short show with pars, moving heads and a rainbow", actions.OpenDemo));
        return list;
    }
}
