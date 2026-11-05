using Avalonia;
using Avalonia.Controls;
using Lampion.App.Controls;
using Lampion.Core.Model.Animation;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Views.Inspector;

/// <summary>
/// A light clip's "Channels" section: lanes that drive fixture channels directly (pan, tilt, strobe, a
/// colour override or a channel number), each an <see cref="AnimatableProperty"/> the user can remove.
/// </summary>
/// <param name="context">The inspector the section belongs to.</param>
internal sealed class ChannelLanesSection(InspectorContext context)
{
    // The "Light" section shows these targets; every other lane belongs here.
    private static readonly AutomationTarget[] LightTargets = [AutomationTarget.Opacity, AutomationTarget.Intensity, AutomationTarget.HueShift, AutomationTarget.Saturation, AutomationTarget.White];

    private static readonly ChannelRole[] Roles = [ChannelRole.Pan, ChannelRole.Tilt, ChannelRole.PanTiltSpeed, ChannelRole.Strobe, ChannelRole.Zoom, ChannelRole.Amber, ChannelRole.UV, ChannelRole.ColorWheel, ChannelRole.Gobo, ChannelRole.Effect];
    private static readonly AutomationTarget[] Overrides = [AutomationTarget.Red, AutomationTarget.Green, AutomationTarget.Blue];

    /// <summary>Adds the section for <paramref name="clip"/> to <paramref name="root"/>.</summary>
    public void Build(Panel root, LightClip clip)
    {
        var addButton = new GlyphButton("+", "Drive a fixture channel directly (pan, tilt, strobe…)", () => { }) { Flyout = AddMenu(clip) };
        var lanes = clip.Automation.Where(l => Array.IndexOf(LightTargets, l.Target) < 0).ToList();
        var section = InspectorLayout.Section(root, "channels", lanes.Count == 0 ? "Channels" : $"Channels ({lanes.Count})", true, addButton);
        if (lanes.Count == 0)
        {
            section.Body.Children.Add(InspectorLayout.Hint("+ to drive pan, tilt, strobe or any channel on every fixture that has it.", new Thickness(20, 2, 0, 4)));
            return;
        }
        foreach (var lane in lanes)
            section.Body.Children.Add(new AnimatableProperty(context, clip, LaneKey.Of(lane), lane.Label, ScrubRange.Percent, removable: true).Row);
    }

    private MenuFlyout AddMenu(LightClip clip)
    {
        var menu = new MenuFlyout();
        foreach (var role in Roles)
        {
            var item = new MenuItem { Header = DisplayNames.Humanize(role.ToString()) };
            item.Click += (_, _) => Add(clip, new LaneKey(AutomationTarget.Role, role));
            menu.Items.Add(item);
        }
        menu.Items.Add(new Separator());
        foreach (var target in Overrides)
        {
            var item = new MenuItem { Header = $"{target} override" };
            item.Click += (_, _) => Add(clip, new LaneKey(target));
            menu.Items.Add(item);
        }
        var byNumber = new MenuItem { Header = "Channel number…" };
        byNumber.Click += async (_, _) =>
        {
            if (context.Owner is not { } owner)
                return;
            var text = await Dialogs.Prompt(owner, "Drive a channel", "Channel within each fixture (1-based)", "1");
            if (int.TryParse(text, out var channel) && channel is >= 1 and <= 512)
                Add(clip, new LaneKey(AutomationTarget.Channel, Channel: channel));
        };
        menu.Items.Add(byNumber);
        return menu;
    }

    private void Add(LightClip clip, LaneKey key)
    {
        if (key.FindIn(clip.Automation) is not null)
            return;
        context.Edit(() =>
        {
            var lane = key.NewLane(animated: false);
            lane.Keys.Add(new Keyframe { Value = key.DefaultValue });
            clip.Automation.Add(lane);
        });
        context.Rebuild();
    }
}
