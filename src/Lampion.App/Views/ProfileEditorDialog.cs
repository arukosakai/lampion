using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lampion.App.Controls;
using Lampion.App.Editor;
using Lampion.Core.Model;

namespace Lampion.App.Views;

/// <summary>
/// Editing one fixture profile: identity and optics on top, colour-coded channels below, and a live 3D
/// preview on the right driven by per-channel test values that are never saved.
/// </summary>
public static class ProfileEditorDialog
{
    private const string RowColumns = "34,*,150,64,150,Auto";

    private static readonly IBrush RowBackground = new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x29));
    private static readonly IBrush PanelBackground = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x22));
    private static readonly IBrush Muted = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A));
    private static readonly IBrush Warning = new SolidColorBrush(Color.FromRgb(0xFF, 0xB0, 0x20));

    /// <returns>True when the user saved (the profile is then in the project).</returns>
    public static async Task<bool> Show(Window owner, EditorState state, FixtureProfile profile, bool isNew)
    {
        var working = profile.Clone(newId: false);
        var test = working.Channels.Select(FullOn).ToList();
        var saved = false;
        var window = new Window
        {
            Title = isNew ? "New Fixture Profile" : $"Fixture Profile — {profile.Manufacturer} {profile.Model}",
            Width = 1240,
            Height = 840,
            MinWidth = 980,
            MinHeight = 560,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        var preview = new FixturePreview { Profile = working };
        var orbit = new ToggleButton { Content = "Orbit", IsChecked = true, Padding = new Thickness(10, 2), CornerRadius = new CornerRadius(3, 0, 0, 3) };
        var aim = new ToggleButton { Content = "◎ Aim", Padding = new Thickness(10, 2), CornerRadius = new CornerRadius(0, 3, 3, 0) };
        ToolTip.SetTip(orbit, "Drag to orbit the camera");
        ToolTip.SetTip(aim, "Click or drag on the floor or wall to point the head there (right-drag still orbits)");
        var modeSwitch = new StackPanel { Orientation = Orientation.Horizontal, Children = { orbit, aim }, VerticalAlignment = VerticalAlignment.Center };
        var hint = new TextBlock { Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };

        void SetAim(bool on)
        {
            preview.AimMode = on;
            orbit.IsChecked = !on;
            aim.IsChecked = on;
            hint.Text = on ? "click the set to aim · right-drag orbits" : "drag to orbit · scroll to zoom";
        }
        orbit.Click += (_, _) => SetAim(false);
        aim.Click += (_, _) => SetAim(true);
        SetAim(false);
        var ribbon = new WrapPanel();
        var channels = new StackPanel { Spacing = 4 };
        var channelCount = new TextBlock { Foreground = Muted, VerticalAlignment = VerticalAlignment.Center };
        var footerNote = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        var panRange = Plain(Ui.Number(working.PanRange, v => working.PanRange = v, 0, 720, 10, "0"));
        var tiltRange = Plain(Ui.Number(working.TiltRange, v => working.TiltRange = v, 0, 360, 10, "0"));
        var users = state.Project.Fixtures.Count(f => f.ProfileId == profile.Id);
        var ribbonFills = new List<Border>();
        var ribbonNumbers = new List<TextBlock>();
        var rowSliders = new List<Slider>();
        var syncingRows = false;
        var patched = state.Project.Fixtures.Where(f => f.ProfileId == profile.Id).ToList();
        IReadOnlySet<string> liveTargets = new HashSet<string>();
        FixtureTester tester = null!;
        tester = new FixtureTester(working, test, () =>
        {
            SyncRows();
            PushTestValues();
        });
        preview.Aimed += tester.SetPanTilt;

        void SyncRows()
        {
            syncingRows = true;
            for (var i = 0; i < rowSliders.Count && i < test.Count; i++)
                rowSliders[i].Value = test[i];
            syncingRows = false;
        }

        void PushTestValues()
        {
            preview.Values = test.ToArray();
            tester.Sync();
            state.Test = liveTargets.Count == 0 ? null : new FixtureTest(liveTargets, test.ToArray());
            for (var i = 0; i < ribbonFills.Count && i < test.Count; i++)
            {
                ribbonFills[i].Height = 30 * test[i] / 255.0;
                // Pale roles (white, dimmer) filled past halfway would swallow a white number.
                var pale = Luminance(ChannelRoleStyle.ColorOf(working.Channels[i].Role)) > 0.7;
                ribbonNumbers[i].Foreground = pale && test[i] > 140 ? Brushes.Black : Brushes.White;
            }
        }

        void Refresh()
        {
            FillChannels();
            FillRibbon();
            PushTestValues();
            channelCount.Text = working.Channels.Count == 1 ? "1 channel" : $"{working.Channels.Count} channels";
            panRange.IsEnabled = tiltRange.IsEnabled = working.HasPanTilt;
            modeSwitch.IsVisible = working.HasPanTilt;
            if (!working.HasPanTilt)
                SetAim(false);
            footerNote.Text = FooterNote();
            footerNote.Foreground = users > 0 && working.Channels.Count != profile.Channels.Count ? Warning : Muted;
        }

        string FooterNote()
        {
            if (users == 0)
                return "";
            var used = $"Used by {users} patched fixture{(users == 1 ? "" : "s")}.";
            return working.Channels.Count == profile.Channels.Count
                ? used
                : $"{used} The footprint changes from {profile.Channels.Count} to {working.Channels.Count} channels, so check the patch for overlaps.";
        }

        void FillRibbon()
        {
            ribbon.Children.Clear();
            ribbonFills.Clear();
            ribbonNumbers.Clear();
            for (var i = 0; i < working.Channels.Count; i++)
            {
                var color = ChannelRoleStyle.ColorOf(working.Channels[i].Role);
                var fill = new Border { Background = new SolidColorBrush(color, 0.75), VerticalAlignment = VerticalAlignment.Bottom };
                ribbonFills.Add(fill);
                var number = new TextBlock { Text = $"{i + 1}", FontSize = 10, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                ribbonNumbers.Add(number);
                var cell = new Border
                {
                    Width = 28,
                    Height = 30,
                    Margin = new Thickness(0, 0, 3, 3),
                    CornerRadius = new CornerRadius(3),
                    ClipToBounds = true,
                    Background = new SolidColorBrush(color, 0.16),
                    BorderBrush = new SolidColorBrush(color, 0.6),
                    BorderThickness = new Thickness(1),
                    Child = new Panel
                    {
                        Children = { fill, number },
                    },
                };
                ToolTip.SetTip(cell, $"{i + 1}: {working.Channels[i].Name} ({ChannelRoleStyle.NameOf(working.Channels[i].Role)})");
                ribbon.Children.Add(cell);
            }
        }

        void FillChannels()
        {
            channels.Children.Clear();
            rowSliders.Clear();
            channels.Children.Add(HeaderRow());
            for (var i = 0; i < working.Channels.Count; i++)
                channels.Children.Add(ChannelRow(i));
            if (working.Channels.Count == 0)
                channels.Children.Add(new TextBlock { Text = "No channels yet. Add the first one below.", Foreground = Muted, Margin = new Thickness(4, 8) });
        }

        Control ChannelRow(int index)
        {
            var channel = working.Channels[index];
            var color = ChannelRoleStyle.BrushOf(channel.Role);
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(RowColumns), ColumnSpacing = 8 };

            grid.Children.Add(new TextBlock { Text = $"{index + 1}", Foreground = color, FontWeight = FontWeight.Bold, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });

            var name = new TextBox { Text = channel.Name, VerticalAlignment = VerticalAlignment.Center };
            name.TextChanged += (_, _) =>
            {
                channel.Name = name.Text ?? "";
                if (index < ribbon.Children.Count)
                    ToolTip.SetTip(ribbon.Children[index], $"{index + 1}: {channel.Name} ({ChannelRoleStyle.NameOf(channel.Role)})");
            };
            Place(grid, name, 1);

            var role = RolePicker(channel.Role);
            role.SelectionChanged += (_, _) =>
            {
                if (role.SelectedItem is not ChannelRole picked || picked == channel.Role)
                    return;
                // Names nobody typed follow the role, so "Channel 3" becomes "Red" when you pick Red.
                if (IsPlaceholderName(channel.Name, channel.Role))
                    channel.Name = ChannelRoleStyle.DefaultName(picked);
                channel.Role = picked;
                test[index] = FullOn(channel);
                // Rebuilding inside the combo's own event would tear it down mid-close.
                Dispatcher.UIThread.Post(Refresh);
            };
            Place(grid, role, 2);

            var def = Plain(Ui.Number(channel.Default, v => channel.Default = (byte)v, 0, 255, 1, "0"));
            def.VerticalAlignment = VerticalAlignment.Center;
            ToolTip.SetTip(def, "Sent when nothing drives this channel (e.g. a shutter that must sit open)");
            Place(grid, def, 3);

            var valueText = new TextBlock { Text = $"{test[index]}", Width = 26, TextAlignment = TextAlignment.Right, Foreground = Muted, VerticalAlignment = VerticalAlignment.Center };
            var slider = new Slider { Minimum = 0, Maximum = 255, Value = test[index], VerticalAlignment = VerticalAlignment.Center };
            foreach (var key in new[] { "SliderThumbBackground", "SliderThumbBackgroundPointerOver", "SliderThumbBackgroundPressed", "SliderTrackValueFill", "SliderTrackValueFillPointerOver", "SliderTrackValueFillPressed" })
                slider.Resources[key] = color;
            slider.ValueChanged += (_, e) =>
            {
                valueText.Text = $"{(byte)Math.Round(e.NewValue)}";
                if (syncingRows)
                    return;
                test[index] = (byte)Math.Round(e.NewValue);
                PushTestValues();
            };
            rowSliders.Add(slider);
            ToolTip.SetTip(slider, "Test value. It drives the preview (and the rig, when output is on) but isn't saved.");
            var testCell = new DockPanel { Children = { valueText, slider } };
            DockPanel.SetDock(valueText, Dock.Right);
            Place(grid, testCell, 4);

            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            actions.Children.Add(IconButton("↑", "Move up", index > 0, () => Swap(index, index - 1)));
            actions.Children.Add(IconButton("↓", "Move down", index < working.Channels.Count - 1, () => Swap(index, index + 1)));
            actions.Children.Add(IconButton("✕", "Remove channel", true, () =>
            {
                working.Channels.RemoveAt(index);
                test.RemoveAt(index);
                Refresh();
            }));
            Place(grid, actions, 5);

            var body = new StackPanel { Spacing = 3, Children = { grid } };
            var firstOfRole = working.Channels.FindIndex(c => c.Role == channel.Role);
            if (channel.Role != ChannelRole.Generic && firstOfRole != index)
                body.Children.Add(new TextBlock
                {
                    Text = $"⚠ Channel {firstOfRole + 1} is already {ChannelRoleStyle.NameOf(channel.Role)}; only the first one is driven, this one stays at its default.",
                    Foreground = Warning,
                    FontSize = 11,
                    Margin = new Thickness(42, 0, 0, 0),
                    TextWrapping = TextWrapping.Wrap,
                });

            return new Border
            {
                Background = RowBackground,
                CornerRadius = new CornerRadius(4),
                BorderBrush = color,
                BorderThickness = new Thickness(4, 0, 0, 0),
                Padding = new Thickness(4, 5, 6, 5),
                Child = body,
            };
        }

        void Swap(int a, int b)
        {
            (working.Channels[a], working.Channels[b]) = (working.Channels[b], working.Channels[a]);
            (test[a], test[b]) = (test[b], test[a]);
            Refresh();
        }

        void AddChannel(ChannelRole role)
        {
            var channel = new FixtureChannel { Name = role == ChannelRole.Generic ? $"Channel {working.Channels.Count + 1}" : ChannelRoleStyle.DefaultName(role), Role = role };
            working.Channels.Add(channel);
            test.Add(FullOn(channel));
            Refresh();
        }

        void SetTest(Func<FixtureChannel, byte> value)
        {
            for (var i = 0; i < test.Count; i++)
                test[i] = value(working.Channels[i]);
            SyncRows();
            PushTestValues();
        }

        // ---- Layout ------------------------------------------------------------------------------------

        var identity = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 10 };
        Place(identity, Labelled("Manufacturer", LiveText(working.Manufacturer, v => working.Manufacturer = v)), 0);
        Place(identity, Labelled("Model", LiveText(working.Model, v => working.Model = v)), 1);
        Place(identity, Labelled("Mode", LiveText(working.Mode, v => working.Mode = v)), 2);

        var look = Ui.Combo(Enum.GetValues<BeamKind>(), working.Beam, v => working.Beam = v);
        var optics = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*"), ColumnSpacing = 10 };
        Place(optics, Labelled("Look in 3D", look), 0);
        Place(optics, Labelled("Beam angle °", Plain(Ui.Number(working.BeamAngle, v => working.BeamAngle = v, 1, 180, 1, "0.#"))), 1);
        Place(optics, Labelled("Pan range °", panRange), 2);
        Place(optics, Labelled("Tilt range °", tiltRange), 3);

        var addMenu = new MenuFlyout();
        foreach (var role in Enum.GetValues<ChannelRole>())
        {
            var item = new MenuItem { Header = RoleChip(role) };
            item.Click += (_, _) => AddChannel(role);
            addMenu.Items.Add(item);
        }
        var add = new Button { Content = "＋ Add channel ▾", Flyout = addMenu, Margin = new Thickness(0, 4, 0, 0) };

        var channelsTitle = Heading("Channels");
        channelsTitle.Margin = new Thickness(0);
        var channelsHeader = new DockPanel { Margin = new Thickness(0, 14, 0, 4) };
        DockPanel.SetDock(channelCount, Dock.Right);
        channelsHeader.Children.Add(channelCount);
        channelsHeader.Children.Add(channelsTitle);

        var editor = new StackPanel
        {
            Spacing = 6,
            Margin = new Thickness(18, 14, 18, 14),
            Children =
            {
                Heading("Identity"),
                identity,
                Heading("Optics"),
                optics,
                channelsHeader,
                channels,
                add,
                new TextBlock
                {
                    Text = "Dimmer, Red, Green, Blue and White are filled from colour on the timeline. Other roles get a lane in Effect Controls → Channels.",
                    Foreground = Muted,
                    FontSize = 11,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 8, 0, 0),
                },
            },
        };

        var testButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                Ui.Button("Full on", () => SetTest(FullOn), "Dimmer and colour at full, everything else at rest"),
                Ui.Button("Defaults", () => SetTest(c => c.Default), "What the fixture shows when nothing drives it"),
                Ui.Button("Blackout", () => SetTest(_ => 0)),
            },
        };

        // Where the test values go besides the preview: nowhere, every patched fixture of this profile, or one of them.
        var outputs = new List<(string Label, HashSet<string> Ids)> { ("Preview only", []) };
        if (patched.Count > 1)
            outputs.Add(($"All {patched.Count} patched fixtures", patched.Select(f => f.Id).ToHashSet()));
        outputs.AddRange(patched.Select(f => ($"{f.Name}  ·  {f.Universe}.{f.Address:000}", new HashSet<string> { f.Id })));
        var output = Ui.Combo(outputs, outputs[0], o =>
        {
            liveTargets = o.Ids;
            PushTestValues();
        }, o => o.Label);
        output.IsEnabled = patched.Count > 0;
        output.MinWidth = 200;
        output.HorizontalAlignment = HorizontalAlignment.Right;
        ToolTip.SetTip(output, patched.Count == 0
            ? "Patch fixtures with this profile to test them live"
            : "Hold these values on the patched fixtures while the editor is open: the stage view shows them, and Art-Net sends them when it's on");
        var testerHeader = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
        var testerTitle = Heading("Tester");
        testerTitle.Margin = new Thickness(0);
        testerTitle.VerticalAlignment = VerticalAlignment.Center;
        var outputLabel = new TextBlock { Text = "Output", Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        var outputRow = new StackPanel { Orientation = Orientation.Horizontal, Children = { outputLabel, output } };
        DockPanel.SetDock(outputRow, Dock.Right);
        testerHeader.Children.Add(outputRow);
        testerHeader.Children.Add(testerTitle);

        var previewFrame = new Border { CornerRadius = new CornerRadius(4), ClipToBounds = true, Child = preview, MinHeight = 260 };
        var previewHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var headerRight = new StackPanel { Orientation = Orientation.Horizontal, Children = { modeSwitch, hint } };
        DockPanel.SetDock(headerRight, Dock.Right);
        previewHeader.Children.Add(headerRight);
        previewHeader.Children.Add(new TextBlock { Text = "Preview", FontWeight = FontWeight.SemiBold, FontSize = 14, VerticalAlignment = VerticalAlignment.Center });

        var previewBottom = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(0, 10, 0, 0),
            Children = { Heading("DMX footprint"), ribbon, testerHeader, tester, testButtons },
        };
        var previewPanel = new DockPanel { Margin = new Thickness(16, 14, 16, 14) };
        DockPanel.SetDock(previewHeader, Dock.Top);
        DockPanel.SetDock(previewBottom, Dock.Bottom);
        previewPanel.Children.Add(previewHeader);
        previewPanel.Children.Add(previewBottom);
        previewPanel.Children.Add(previewFrame);

        var ok = new Button { Content = isNew ? "Create" : "Save", MinWidth = 96, IsDefault = true };
        ok.Classes.Add("accent");
        var cancel = new Button { Content = "Cancel", MinWidth = 96, IsCancel = true };
        cancel.Click += (_, _) => window.Close();
        ok.Click += (_, _) =>
        {
            var index = state.Project.Profiles.FindIndex(p => p.Id == working.Id);
            if (index >= 0)
                state.Project.Profiles[index] = working;
            else
                state.Project.Profiles.Add(working);
            state.Commit();
            saved = true;
            window.Close();
        };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { cancel, ok } };
        var footer = new DockPanel { Margin = new Thickness(18, 10), LastChildFill = true };
        DockPanel.SetDock(buttons, Dock.Right);
        footer.Children.Add(buttons);
        footer.Children.Add(footerNote);
        var footerBar = new Border { Background = PanelBackground, BorderBrush = Brushes.Black, BorderThickness = new Thickness(0, 1, 0, 0), Child = footer };

        // However the window goes (Save, Cancel, ✕), the patched fixtures go back to the show.
        window.Closed += (_, _) => state.Test = null;

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("*,500") };
        var scroller = new ScrollViewer { Content = editor };
        body.Children.Add(scroller);
        // Opening focuses the last field, and the scroller follows focus; start at the top instead.
        window.Opened += (_, _) => Dispatcher.UIThread.Post(() => scroller.Offset = default, DispatcherPriority.Background);
        var side = new Border { Background = PanelBackground, BorderBrush = Brushes.Black, BorderThickness = new Thickness(1, 0, 0, 0), Child = previewPanel };
        Place(body, side, 1);

        var root = new DockPanel();
        DockPanel.SetDock(footerBar, Dock.Bottom);
        root.Children.Add(footerBar);
        root.Children.Add(body);
        window.Content = root;

        Refresh();
        await window.ShowDialog(owner);
        return saved;
    }

    /// <summary>A test value that makes the preview light up: intensity and colour at full, movement centred.</summary>
    private static byte FullOn(FixtureChannel channel) =>
        channel.Role switch
        {
            ChannelRole.Dimmer or ChannelRole.Red or ChannelRole.Green or ChannelRole.Blue or ChannelRole.White => 255,
            ChannelRole.Pan or ChannelRole.Tilt => 128,
            ChannelRole.Amber or ChannelRole.UV or ChannelRole.Strobe or ChannelRole.PanFine or ChannelRole.TiltFine => 0,
            _ => channel.Default,
        };

    // Spinner buttons make number boxes taller than the text boxes beside them; scroll and arrow keys still step.
    private static NumericUpDown Plain(NumericUpDown box)
    {
        box.ShowButtonSpinner = false;
        return box;
    }

    private static double Luminance(Color c) => (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255;

    private static bool IsPlaceholderName(string name, ChannelRole role) =>
        string.IsNullOrWhiteSpace(name) || name == ChannelRoleStyle.DefaultName(role) || name.StartsWith("Channel ", StringComparison.Ordinal) || name.StartsWith("Ch ", StringComparison.Ordinal);

    private static Control HeaderRow()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(RowColumns), ColumnSpacing = 8, Margin = new Thickness(8, 0, 6, 0) };
        string[] titles = ["#", "Name", "Role", "Default", "Preview value"];
        for (var i = 0; i < titles.Length; i++)
            Place(grid, new TextBlock { Text = titles[i], Foreground = Muted, FontSize = 11, HorizontalAlignment = i == 0 ? HorizontalAlignment.Center : HorizontalAlignment.Left }, i);
        return grid;
    }

    private static ComboBox RolePicker(ChannelRole selected) =>
        new()
        {
            ItemsSource = Enum.GetValues<ChannelRole>(),
            SelectedItem = selected,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            ItemTemplate = new FuncDataTemplate<ChannelRole>((role, _) => RoleChip(role)),
            MaxDropDownHeight = 420,
        };

    private static Control RoleChip(ChannelRole role) =>
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

    private static Button IconButton(string glyph, string tip, bool enabled, Action onClick)
    {
        var button = new Button
        {
            Content = glyph,
            Width = 24,
            MinHeight = 24,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Background = Brushes.Transparent,
            // Faded instead of disabled: Fluent paints disabled buttons a solid grey block.
            Opacity = enabled ? 1 : 0.2,
            IsHitTestVisible = enabled,
        };
        ToolTip.SetTip(button, tip);
        button.Click += (_, _) => onClick();
        return button;
    }

    private static TextBlock Heading(string text) =>
        new() { Text = text.ToUpperInvariant(), Foreground = Muted, FontSize = 10.5, FontWeight = FontWeight.SemiBold, LetterSpacing = 0.8, Margin = new Thickness(0, 8, 0, 0) };

    private static Control Labelled(string label, Control editor) =>
        new StackPanel { Spacing = 3, Children = { new TextBlock { Text = label, Foreground = Muted, FontSize = 11 }, editor } };

    // Live rather than commit-on-blur: the working copy is only kept if Save is pressed anyway.
    private static TextBox LiveText(string value, Action<string> set)
    {
        var box = new TextBox { Text = value };
        box.TextChanged += (_, _) => set(box.Text ?? "");
        return box;
    }

    private static void Place(Grid grid, Control control, int column)
    {
        Grid.SetColumn(control, column);
        grid.Children.Add(control);
    }
}
