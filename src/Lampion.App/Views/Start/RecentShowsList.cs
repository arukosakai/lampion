using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lampion.App.Editor;

namespace Lampion.App.Views.Start;

/// <summary>The start screen's recent shows: one card per file, with its date and a button to forget it.</summary>
internal sealed class RecentShowsList : StackPanel
{
    private const int Shown = 8;

    private readonly StartActions _actions;

    /// <summary>The recent shows as they are on disk now; clicking one opens it through <paramref name="actions"/>.</summary>
    public RecentShowsList(StartActions actions)
    {
        _actions = actions;
        Spacing = 6;
        Fill();
    }

    /// <summary>Rebuilds the list from the recent files.</summary>
    public void Fill()
    {
        Children.Clear();
        var recent = UserData.RecentProjects();
        if (recent.Count == 0)
        {
            Children.Add(new TextBlock { Text = "Nothing yet — shows you open or save will appear here.", Foreground = Brushes.Gray });
            return;
        }
        foreach (var path in recent.Take(Shown))
            Children.Add(Row(path));
    }

    private Border Row(string path)
    {
        var exists = File.Exists(path);
        var when = exists ? File.GetLastWriteTime(path).ToString("g") : "file not found";
        var remove = new Button { Content = "✕", Padding = new Thickness(6, 0), MinHeight = 22, VerticalAlignment = VerticalAlignment.Center, Background = Brushes.Transparent };
        ToolTip.SetTip(remove, "Remove from the list");
        remove.Click += (_, e) =>
        {
            e.Handled = true;
            UserData.RemoveRecent(path);
            Fill();
        };
        var text = new StackPanel
        {
            Children =
            {
                new TextBlock { Text = Path.GetFileNameWithoutExtension(path), FontWeight = FontWeight.SemiBold, Foreground = exists ? Brushes.White : Brushes.Gray },
                new TextBlock { Text = path, Foreground = Brushes.Gray, FontSize = 11, TextTrimming = TextTrimming.PathSegmentEllipsis },
            },
        };
        var date = new TextBlock { Text = when, Foreground = exists ? Brushes.Gray : Brushes.OrangeRed, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0) };
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
        Grid.SetColumn(date, 1);
        Grid.SetColumn(remove, 2);
        row.Children.Add(text);
        row.Children.Add(date);
        row.Children.Add(remove);
        return StartCard.Create(row, () =>
        {
            if (exists)
                _actions.Open(path);
            else
                _actions.ShowError($"{path} no longer exists. Remove it with ✕, or open it from its new location.");
        });
    }
}
