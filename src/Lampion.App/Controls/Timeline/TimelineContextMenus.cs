using Avalonia.Controls;
using Avalonia.Input;
using Lampion.App.Editor;
using Lampion.App.Views;
using Lampion.Core.Model.Animation;
using Lampion.Core.Model.Effects;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Controls.Timeline;

/// <summary>The right-click menus for clips and tracks. Built fresh on each click (cold path).</summary>
internal sealed class TimelineContextMenus(EditorState state, TimelineSnapper snapper, TimelineControl host)
{
    private static readonly AutomationTarget[] LaneTargets =
        [AutomationTarget.HueShift, AutomationTarget.Saturation, AutomationTarget.Intensity, AutomationTarget.Opacity, AutomationTarget.White];

    private static readonly ChannelRole[] LaneRoles =
        [ChannelRole.Pan, ChannelRole.Tilt, ChannelRole.Strobe, ChannelRole.Zoom, ChannelRole.Amber, ChannelRole.UV, ChannelRole.Gobo, ChannelRole.Effect, ChannelRole.PanTiltSpeed];

    /// <summary>The menu for <paramref name="clip"/> on <paramref name="track"/>: razor, duplicate, delete, and for light clips transitions, automation and presets.</summary>
    public ContextMenu ClipMenu(Clip clip, Track track)
    {
        var menu = new ContextMenu();
        menu.Items.Add(Item("Split at playhead\tCtrl+K", () => TimelineOps.SplitAtPlayhead(state)));
        menu.Items.Add(Item("Duplicate\tCtrl+D", () => TimelineOps.Duplicate(state)));
        menu.Items.Add(Item("Delete\tDelete", () => TimelineOps.DeleteSelection(state)));
        if (clip is not LightClip light)
            return menu;

        menu.Items.Add(new Separator());
        var transition = new MenuItem { Header = "Transition in" };
        foreach (var kind in Enum.GetValues<TransitionKind>())
            transition.Items.Add(Item(kind.ToString(), () => TimelineOps.SetTransition(state, light, kind)));
        transition.Items.Add(Item("None", () => TimelineOps.SetTransition(state, light, null)));
        menu.Items.Add(transition);

        var lanes = new MenuItem { Header = "Add automation" };
        foreach (var target in LaneTargets)
            lanes.Items.Add(Item(target == AutomationTarget.HueShift ? "Hue shift (rainbow)" : target.ToString(), () => AddLane(light, track, target)));
        var roles = new MenuItem { Header = "Channel role" };
        foreach (var role in LaneRoles)
            roles.Items.Add(Item(role.ToString(), () => AddLane(light, track, AutomationTarget.Role, role)));
        lanes.Items.Add(roles);
        menu.Items.Add(lanes);

        menu.Items.Add(new Separator());
        var apply = new MenuItem { Header = "Apply preset" };
        foreach (var (_, preset) in PresetActions.Library())
            apply.Items.Add(Item(preset.Name, () => TimelineOps.ApplyPreset(state, light, preset)));
        menu.Items.Add(apply);
        menu.Items.Add(Item("Save as preset…", () =>
        {
            if (TopLevel.GetTopLevel(host) is Window owner)
                _ = PresetActions.SaveFromClip(owner, light);
        }));
        return menu;
    }

    /// <summary>The menu for <paramref name="track"/>; clips it adds start at <paramref name="time"/> seconds (snapped).</summary>
    public ContextMenu TrackMenu(Track track, double time)
    {
        var menu = new ContextMenu();
        if (track.Kind == TrackKind.Light)
        {
            var add = new MenuItem { Header = "Add effect here" };
            foreach (var (kind, name, _) in EffectCatalog.Entries)
                add.Items.Add(Item(name, () => TimelineOps.AddEffectClip(state, track, kind, snapper.SnapDrag(Math.Max(0, time)))));
            menu.Items.Add(add);
            var addPreset = new MenuItem { Header = "Add preset here" };
            foreach (var (_, preset) in PresetActions.Library())
                addPreset.Items.Add(Item(preset.Name, () => TimelineOps.AddPresetClip(state, track, preset, snapper.SnapDrag(Math.Max(0, time)))));
            menu.Items.Add(addPreset);
        }
        menu.Items.Add(Item("New light track", () => TimelineOps.AddLightTrack(state, index: state.Project.Tracks.IndexOf(track))));
        menu.Items.Add(Item("Move up", () => TrackEdits.Move(state, track, -1)));
        menu.Items.Add(Item("Move down", () => TrackEdits.Move(state, track, 1)));
        menu.Items.Add(Item(track.Locked ? "Unlock" : "Lock", () => TrackEdits.ToggleLock(state, track)));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Delete track", () => TrackEdits.Delete(state, track)));
        return menu;
    }

    private void AddLane(LightClip clip, Track track, AutomationTarget target, ChannelRole role = ChannelRole.Strobe)
    {
        TimelineOps.AddLane(state, clip, target, role);
        track.Expanded = true;
        state.Touch();
        host.NotifyViewChanged();
    }

    /// <summary>A menu item; text after a tab is shown as its shortcut.</summary>
    private static MenuItem Item(string header, Action action)
    {
        var parts = header.Split('\t');
        var item = new MenuItem { Header = parts[0] };
        if (parts.Length > 1)
            item.InputGesture = KeyGesture.Parse(parts[1]);
        item.Click += (_, _) => action();
        return item;
    }
}
