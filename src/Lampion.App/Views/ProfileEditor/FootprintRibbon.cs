using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lampion.Core.Model.Rig;

namespace Lampion.App.Views.ProfileEditor;

/// <summary>
/// The DMX footprint: one numbered cell per channel in its role's colour, filled from the bottom by its test
/// value. Updating the fill on a value change allocates nothing.
/// </summary>
internal sealed class FootprintRibbon : WrapPanel
{
    private const double CellHeight = 30;

    private readonly ProfileEditSession _session;
    private readonly List<Border> _fills = [];
    private readonly List<TextBlock> _numbers = [];
    private readonly List<bool> _pale = [];

    /// <param name="session">The editor session whose channels and test values are shown.</param>
    public FootprintRibbon(ProfileEditSession session)
    {
        _session = session;
        session.ChannelsChanged += Rebuild;
        session.ValuesChanged += ShowValues;
        session.ChannelRenamed += UpdateTip;
    }

    /// <summary>Rebuilds one cell per channel of the working profile.</summary>
    public void Rebuild()
    {
        var channels = _session.Working.Channels;
        Children.Clear();
        _fills.Clear();
        _numbers.Clear();
        _pale.Clear();
        for (var i = 0; i < channels.Count; i++)
        {
            var color = ChannelRoleStyle.ColorOf(channels[i].Role);
            var fill = new Border { Background = new SolidColorBrush(color, 0.75), VerticalAlignment = VerticalAlignment.Bottom };
            var number = new TextBlock { Text = $"{i + 1}", FontSize = 10, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            _fills.Add(fill);
            _numbers.Add(number);
            _pale.Add(Luminance(color) > 0.7);
            var cell = new Border
            {
                Width = 28,
                Height = CellHeight,
                Margin = new Thickness(0, 0, 3, 3),
                CornerRadius = new CornerRadius(3),
                ClipToBounds = true,
                Background = new SolidColorBrush(color, 0.16),
                BorderBrush = new SolidColorBrush(color, 0.6),
                BorderThickness = new Thickness(1),
                Child = new Panel { Children = { fill, number } },
            };
            ToolTip.SetTip(cell, Tip(i, channels[i]));
            Children.Add(cell);
        }
    }

    private void ShowValues()
    {
        var test = _session.Test;
        for (var i = 0; i < _fills.Count && i < test.Count; i++)
        {
            _fills[i].Height = CellHeight * test[i] / 255.0;
            // Pale roles (white, dimmer) filled past halfway would swallow a white number.
            _numbers[i].Foreground = _pale[i] && test[i] > 140 ? Brushes.Black : Brushes.White;
        }
    }

    private void UpdateTip(int index)
    {
        if (index < Children.Count)
            ToolTip.SetTip(Children[index], Tip(index, _session.Working.Channels[index]));
    }

    private static string Tip(int index, FixtureChannel channel) => $"{index + 1}: {channel.Name} ({ChannelRoleStyle.NameOf(channel.Role)})";

    private static double Luminance(Color c) => (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255;
}
