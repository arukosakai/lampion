using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Lampion.Core.Model.Rig;

namespace Lampion.App.Views.ProfileEditor;

/// <summary>Colour-dot-and-name chips for channel roles, and the role picker built from them.</summary>
internal static class RoleChips
{
    private static readonly ChannelRole[] Roles = Enum.GetValues<ChannelRole>();

    /// <summary>Every role, in declaration order, as the add menu and pickers list them.</summary>
    public static IReadOnlyList<ChannelRole> All => Roles;

    /// <summary>A new chip: the role's colour dot followed by its display name.</summary>
    public static Control Chip(ChannelRole role) =>
        new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                new Ellipse { Width = 10, Height = 10, Fill = ChannelRoleStyle.BrushOf(role), VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = ChannelRoleStyle.NameOf(role), VerticalAlignment = VerticalAlignment.Center },
            },
        };

    /// <summary>A combo of every role shown as chips, with <paramref name="selected"/> picked.</summary>
    public static ComboBox Picker(ChannelRole selected) =>
        new()
        {
            ItemsSource = Roles,
            SelectedItem = selected,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            ItemTemplate = new FuncDataTemplate<ChannelRole>((role, _) => Chip(role)),
            MaxDropDownHeight = 420,
        };
}
