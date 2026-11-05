using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lampion.Core.Model.Rig;

namespace Lampion.App.Views.NewFixtures;

/// <summary>What the selected profile is: name, optics and its channel table, as Premiere describes a preset.</summary>
internal sealed class ProfileDetailsView : StackPanel
{
    /// <summary>Creates an empty view; call <see cref="Show"/>.</summary>
    public ProfileDetailsView() => Spacing = 4;

    /// <summary>Describes <paramref name="profile"/>, or shows nothing for null.</summary>
    public void Show(FixtureProfile? profile)
    {
        Children.Clear();
        if (profile is null)
            return;
        var movement = profile.HasPanTilt ? $" · pan {profile.PanRange:0}° / tilt {profile.TiltRange:0}°" : "";
        Children.Add(new TextBlock { Text = $"{profile.Manufacturer} {profile.Model}", FontSize = 15, FontWeight = FontWeight.SemiBold });
        Children.Add(new TextBlock { Text = $"{profile.Mode} · {profile.Beam} · {profile.BeamAngle:0.#}° beam{movement}", Foreground = Brushes.Gray, FontSize = 11 });
        Children.Add(new ScrollViewer { Content = ChannelTable(profile), MaxHeight = 220 });
    }

    private static Grid ChannelTable(FixtureProfile profile)
    {
        var table = new Grid { ColumnDefinitions = new ColumnDefinitions("34,*,*,50"), Margin = new Thickness(0, 8, 0, 0) };
        table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Cell(table, "Ch", 0, 0, Brushes.Gray);
        Cell(table, "Name", 0, 1, Brushes.Gray);
        Cell(table, "Role", 0, 2, Brushes.Gray);
        Cell(table, "Default", 0, 3, Brushes.Gray);
        for (var i = 0; i < profile.Channels.Count; i++)
        {
            table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var c = profile.Channels[i];
            Cell(table, $"{i + 1}", i + 1, 0);
            Cell(table, c.Name, i + 1, 1);
            Cell(table, c.Role.ToString(), i + 1, 2, RoleBrush(c.Role));
            Cell(table, $"{c.Default}", i + 1, 3);
        }
        return table;
    }

    private static IBrush? RoleBrush(ChannelRole role) =>
        role switch
        {
            ChannelRole.Red => Brushes.IndianRed,
            ChannelRole.Green => Brushes.LightGreen,
            ChannelRole.Blue => Brushes.CornflowerBlue,
            _ => null,
        };

    private static void Cell(Grid table, string text, int row, int column, IBrush? brush = null)
    {
        var block = new TextBlock { Text = text, Foreground = brush ?? Brushes.Gainsboro, FontSize = 12, Margin = new Thickness(0, 1) };
        Grid.SetRow(block, row);
        Grid.SetColumn(block, column);
        table.Children.Add(block);
    }
}
