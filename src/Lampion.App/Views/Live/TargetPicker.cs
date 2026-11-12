using Avalonia.Controls;
using Lampion.Core.Model;

namespace Lampion.App.Views.Live;

/// <summary>A tick list of the project's groups and fixtures, editing two id lists in place.</summary>
internal static class TargetPicker
{
    /// <summary>A scrollable list; ticking a row adds its id to <paramref name="groupIds"/> or <paramref name="fixtureIds"/>.</summary>
    public static Control Build(Project project, List<string> fixtureIds, List<string> groupIds)
    {
        var rows = new StackPanel { Spacing = 1 };
        if (project.Groups.Count > 0)
            rows.Children.Add(Ui.Section("Groups"));
        foreach (var group in project.Groups)
            rows.Children.Add(Row(group.Name, group.Id, groupIds));
        rows.Children.Add(Ui.Section("Fixtures"));
        foreach (var fixture in project.Fixtures)
            rows.Children.Add(Row($"{fixture.Name}  ·  {fixture.Universe}.{fixture.Address:000}", fixture.Id, fixtureIds));
        return new ScrollViewer { Content = rows, MaxHeight = 260 };
    }

    private static CheckBox Row(string text, string id, List<string> ids)
    {
        var box = new CheckBox { Content = text, IsChecked = ids.Contains(id) };
        box.IsCheckedChanged += (_, _) =>
        {
            ids.Remove(id);
            if (box.IsChecked == true)
                ids.Add(id);
        };
        return box;
    }
}
