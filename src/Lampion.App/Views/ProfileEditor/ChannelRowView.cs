using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lampion.Core.Model.Rig;

namespace Lampion.App.Views.ProfileEditor;

/// <summary>
/// One channel in the profile editor: number, name, role, default and a test-value slider, with move and
/// remove buttons, tinted in the role's colour. Warns when an earlier channel already has the same role.
/// </summary>
internal sealed class ChannelRowView : Border
{
    /// <summary>Grid columns shared by the rows and the header above them.</summary>
    public const string Columns = "34,*,150,64,150,Auto";

    private static readonly string[] SliderColourKeys =
    [
        "SliderThumbBackground",
        "SliderThumbBackgroundPointerOver",
        "SliderThumbBackgroundPressed",
        "SliderTrackValueFill",
        "SliderTrackValueFillPointerOver",
        "SliderTrackValueFillPressed",
    ];

    private readonly ProfileEditSession _session;
    private readonly int _index;
    private readonly Slider _slider;
    private readonly TextBlock _valueText;
    private bool _showing;

    /// <param name="session">The editor session the row edits through.</param>
    /// <param name="index">0-based channel index; rows are rebuilt whenever channels move.</param>
    public ChannelRowView(ProfileEditSession session, int index)
    {
        _session = session;
        _index = index;
        var channels = session.Working.Channels;
        var channel = channels[index];
        var color = ChannelRoleStyle.BrushOf(channel.Role);
        var value = session.Test[index];

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(Columns), ColumnSpacing = 8 };
        grid.Children.Add(new TextBlock { Text = $"{index + 1}", Foreground = color, FontWeight = FontWeight.Bold, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });

        var name = new TextBox { Text = channel.Name, VerticalAlignment = VerticalAlignment.Center };
        name.TextChanged += (_, _) => session.RenameChannel(index, name.Text ?? "");
        ProfileEditorUi.Place(grid, name, 1);

        var role = RoleChips.Picker(channel.Role);
        role.SelectionChanged += (_, _) =>
        {
            if (role.SelectedItem is ChannelRole picked && picked != channel.Role)
                session.ChangeRole(index, picked);
        };
        ProfileEditorUi.Place(grid, role, 2);

        var def = ProfileEditorUi.Plain(Ui.Number(channel.Default, v => channel.Default = (byte)v, 0, 255, 1, "0"));
        def.VerticalAlignment = VerticalAlignment.Center;
        ToolTip.SetTip(def, "Sent when nothing drives this channel (e.g. a shutter that must sit open)");
        ProfileEditorUi.Place(grid, def, 3);

        _valueText = new TextBlock { Text = NumberLabels.Dmx.Of(value), Width = 26, TextAlignment = TextAlignment.Right, Foreground = ProfileEditorUi.Muted, VerticalAlignment = VerticalAlignment.Center };
        _slider = new Slider { Minimum = 0, Maximum = 255, Value = value, VerticalAlignment = VerticalAlignment.Center };
        foreach (var key in SliderColourKeys)
            _slider.Resources[key] = color;
        _slider.ValueChanged += (_, e) => OnSliderChanged(e.NewValue);
        ToolTip.SetTip(_slider, "Test value. It drives the preview (and the rig, when output is on) but isn't saved.");
        var testCell = new DockPanel { Children = { _valueText, _slider } };
        DockPanel.SetDock(_valueText, Dock.Right);
        ProfileEditorUi.Place(grid, testCell, 4);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        actions.Children.Add(ProfileEditorUi.IconButton("↑", "Move up", index > 0, () => session.SwapChannels(index, index - 1)));
        actions.Children.Add(ProfileEditorUi.IconButton("↓", "Move down", index < channels.Count - 1, () => session.SwapChannels(index, index + 1)));
        actions.Children.Add(ProfileEditorUi.IconButton("✕", "Remove channel", true, () => session.RemoveChannel(index)));
        ProfileEditorUi.Place(grid, actions, 5);

        var body = new StackPanel { Spacing = 3, Children = { grid } };
        var firstOfRole = channels.FindIndex(c => c.Role == channel.Role);
        if (channel.Role != ChannelRole.Generic && firstOfRole != index)
            body.Children.Add(DuplicateRoleWarning(firstOfRole, channel.Role));

        Background = ProfileEditorUi.RowBackground;
        CornerRadius = new CornerRadius(4);
        BorderBrush = color;
        BorderThickness = new Thickness(4, 0, 0, 0);
        Padding = new Thickness(4, 5, 6, 5);
        Child = body;
    }

    /// <summary>
    /// Moves the slider to <paramref name="value"/> after the tester or a preset button changed it. Leaves the
    /// slider alone when it already rounds to that value, so a drag in progress isn't snapped.
    /// </summary>
    public void ShowValue(byte value)
    {
        if ((byte)Math.Round(_slider.Value) == value)
            return;
        _showing = true;
        _slider.Value = value;
        _showing = false;
    }

    private void OnSliderChanged(double raw)
    {
        var value = (byte)Math.Round(raw);
        _valueText.Text = NumberLabels.Dmx.Of(value);
        if (!_showing)
            _session.SetTestValue(_index, value);
    }

    private static TextBlock DuplicateRoleWarning(int firstOfRole, ChannelRole role) =>
        new()
        {
            Text = $"⚠ Channel {firstOfRole + 1} is already {ChannelRoleStyle.NameOf(role)}; only the first one is driven, this one stays at its default.",
            Foreground = ProfileEditorUi.Warning,
            FontSize = 11,
            Margin = new Thickness(42, 0, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        };
}
