using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Lampion.App.Controls;
using Lampion.App.Editor;
using Lampion.Core.Model;
using LightEffect = Lampion.Core.Model.Effect;

namespace Lampion.App.Views;

/// <summary>The left dock: fixtures and groups, fixture profiles, imported media and the effects library.</summary>
public sealed class ProjectPanel : TabControl
{
    private readonly EditorState _state;
    private readonly TimelineControl _timeline;
    private readonly ListBox _fixtures = new() { SelectionMode = SelectionMode.Multiple | SelectionMode.Toggle };
    private readonly StackPanel _groups = new() { Spacing = 2 };
    private readonly TextBlock _problems = new() { Foreground = Brushes.Orange, TextWrapping = TextWrapping.Wrap, FontSize = 11 };
    private readonly ContentControl _profiles = new();
    private readonly StackPanel _media = new() { Spacing = 2 };
    private string _fixtureSignature = "";
    private string? _profileId;
    private bool _syncingSelection;

    protected override Type StyleKeyOverride => typeof(TabControl);

    public ProjectPanel(EditorState state, TimelineControl timeline, Func<Task> importAudio, Func<Task> importVideo)
    {
        _state = state;
        _timeline = timeline;
        Items.Add(new TabItem { Header = "Fixtures", Content = Scroll(FixturesTab()) });
        Items.Add(new TabItem { Header = "Profiles", Content = Scroll(_profiles) });
        Items.Add(new TabItem { Header = "Media", Content = Scroll(MediaTab(importAudio, importVideo)) });
        Items.Add(new TabItem { Header = "Effects", Content = Scroll(EffectsTab()) });

        _fixtures.SelectionChanged += (_, _) =>
        {
            if (_syncingSelection)
                return;
            var ids = _fixtures.SelectedItems?.OfType<ListBoxItem>().Select(i => (string)i.Tag!).ToList() ?? [];
            if (ids.Count > 0)
                state.SelectFixtures(ids);
        };
        state.Changed += Refresh;
        state.ProjectReplaced += () =>
        {
            _fixtureSignature = "";
            _profileId = null;
            Refresh();
        };
        state.SelectionChanged += SyncFixtureSelection;
        Refresh();
    }

    private static ScrollViewer Scroll(Control content) => new() { Content = new Border { Padding = new Thickness(8), Child = content } };

    private void Refresh()
    {
        RefreshFixtures();
        RefreshGroups();
        if (!_profiles.IsKeyboardFocusWithin)
            RefreshProfiles();
        RefreshMedia();
        var problems = _state.Project.PatchProblems();
        _problems.Text = string.Join("\n", problems.Select(p => "⚠ " + p));
        _problems.IsVisible = problems.Count > 0;
    }

    // ---- Fixtures --------------------------------------------------------------------------------------

    private Control FixturesTab()
    {
        var profile = Ui.Combo<FixtureProfile>(_state.Project.Profiles, _state.Project.Profiles.FirstOrDefault()!, _ => { }, p => p.DisplayName);
        _state.ProjectReplaced += () => profile.ItemsSource = _state.Project.Profiles.Select(p => p.DisplayName).ToList();
        _state.Changed += () =>
        {
            var names = _state.Project.Profiles.Select(p => p.DisplayName).ToList();
            if (profile.ItemsSource is List<string> current && current.SequenceEqual(names))
                return;
            var index = profile.SelectedIndex;
            profile.ItemsSource = names;
            profile.SelectedIndex = Math.Clamp(index, 0, names.Count - 1);
        };
        var count = Ui.Number(4, _ => { }, 1, 128, 1, "0");
        var universe = Ui.Number(1, _ => { }, 1, 64, 1, "0");

        var add = Ui.Button("Patch", () =>
        {
            if (profile.SelectedIndex < 0 || profile.SelectedIndex >= _state.Project.Profiles.Count)
                return;
            AddFixtures(_state.Project.Profiles[profile.SelectedIndex], (int)(count.Value ?? 1), (int)(universe.Value ?? 1));
        }, "Add fixtures at the next free addresses");

        var groupButtons = Ui.Row(
            Ui.Button("+ Group", MakeGroup, "Group the selected fixtures"),
            Ui.Button("Duplicate", DuplicateFixtures, "Duplicate the selected fixtures at free addresses"),
            Ui.Button("Delete", () => TimelineOps.DeleteSelection(_state))
        );

        return Ui.Column(
            4,
            Ui.Section("Patch fixtures"),
            Ui.Field("Profile", profile, 60),
            Ui.Field("Count", count, 60),
            Ui.Field("Universe", universe, 60),
            add,
            _problems,
            Ui.Section("Fixtures"),
            new Border { Child = _fixtures, MaxHeight = 360, BorderBrush = Brushes.Black, BorderThickness = new Thickness(1) },
            groupButtons,
            Ui.Section("Groups"),
            _groups
        );
    }

    private void AddFixtures(FixtureProfile profile, int count, int universe)
    {
        var added = new List<string>();
        var startX = -(count - 1) / 2.0;
        for (var i = 0; i < count; i++)
        {
            var u = universe;
            var address = _state.Project.NextFreeAddress(u, profile.Channels.Count);
            while (address < 0 && u < 64)
                address = _state.Project.NextFreeAddress(++u, profile.Channels.Count);
            if (address < 0)
                break;
            var fixture = new Fixture
            {
                Name = $"{profile.Model} {_state.Project.Fixtures.Count(f => f.ProfileId == profile.Id) + 1}",
                ProfileId = profile.Id,
                Universe = u,
                Address = address,
                Position = new Vec3(startX + i, 5.5, 0),
                Pitch = profile.HasPanTilt ? -90 : -60,
            };
            _state.Project.Fixtures.Add(fixture);
            added.Add(fixture.Id);
        }
        _state.Commit();
        _state.SelectFixtures(added);
    }

    private void DuplicateFixtures()
    {
        var added = new List<string>();
        foreach (var fixture in _state.SelectedFixtureIds.Select(_state.Project.FixtureById).OfType<Fixture>().ToList())
        {
            var profile = _state.Project.ProfileOf(fixture);
            var copy = fixture.Clone();
            copy.Name = fixture.Name + " copy";
            copy.Address = _state.Project.NextFreeAddress(copy.Universe, profile?.Channels.Count ?? 1);
            if (copy.Address < 0)
                continue;
            copy.Position = fixture.Position + new Vec3(0.5, 0, 0);
            _state.Project.Fixtures.Add(copy);
            added.Add(copy.Id);
        }
        _state.Commit();
        _state.SelectFixtures(added);
    }

    private void MakeGroup()
    {
        if (_state.SelectedFixtureIds.Count == 0)
            return;
        string[] palette = ["#E6A23A", "#3AA0E6", "#E63A8E", "#5BD16A", "#B05BE6", "#E6E03A"];
        var group = new FixtureGroup
        {
            Name = $"Group {_state.Project.Groups.Count + 1}",
            Color = palette[_state.Project.Groups.Count % palette.Length],
            FixtureIds = [.. _state.Project.Fixtures.Where(f => _state.SelectedFixtureIds.Contains(f.Id)).Select(f => f.Id)],
        };
        _state.Project.Groups.Add(group);
        _state.Commit();
    }

    private void RefreshFixtures()
    {
        var signature = string.Join("|", _state.Project.Fixtures.Select(f => $"{f.Id}{f.Name}{f.Universe}.{f.Address}{f.ProfileId}"));
        if (signature == _fixtureSignature)
            return;
        _fixtureSignature = signature;
        _syncingSelection = true;
        _fixtures.Items.Clear();
        foreach (var fixture in _state.Project.Fixtures.OrderBy(f => f.Universe).ThenBy(f => f.Address))
        {
            var profile = _state.Project.ProfileOf(fixture);
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            row.Children.Add(new TextBlock { Text = fixture.Name, TextTrimming = TextTrimming.CharacterEllipsis });
            var addr = new TextBlock
            {
                Text = $"{fixture.Universe}.{fixture.Address:000}  {profile?.Channels.Count ?? 0}ch",
                Foreground = Brushes.Gray,
                FontSize = 11,
                Margin = new Thickness(8, 0, 0, 0),
            };
            Grid.SetColumn(addr, 1);
            row.Children.Add(addr);
            _fixtures.Items.Add(new ListBoxItem { Content = row, Tag = fixture.Id });
        }
        _syncingSelection = false;
        SyncFixtureSelection();
    }

    private void SyncFixtureSelection()
    {
        _syncingSelection = true;
        foreach (var item in _fixtures.Items.OfType<ListBoxItem>())
            item.IsSelected = _state.SelectedFixtureIds.Contains((string)item.Tag!);
        _syncingSelection = false;
    }

    private void RefreshGroups()
    {
        if (_groups.IsKeyboardFocusWithin)
            return;
        _groups.Children.Clear();
        foreach (var group in _state.Project.Groups)
        {
            var swatch = Ui.ColorPicker(group.Color, hex => group.Color = hex, () => _state.Commit());
            var name = Ui.Text(group.Name, v =>
            {
                group.Name = v;
                _state.Commit();
            });
            name.Width = 110;
            var select = Ui.Button($"{group.FixtureIds.Count}", () => _state.SelectFixtures(group.FixtureIds), "Select the group's fixtures");
            var track = Ui.Button("→ Track", () =>
            {
                var t = TimelineOps.AddLightTrack(_state, groupIds: [group.Id]);
                t.Name = group.Name;
                _state.Commit();
            }, "New light track driving this group");
            var delete = Ui.Button("✕", () =>
            {
                _state.Project.Groups.Remove(group);
                foreach (var t in _state.Project.Tracks)
                    t.GroupIds.Remove(group.Id);
                _state.Commit();
            });
            var row = Ui.Row(swatch, name, select, track, delete);
            _groups.Children.Add(row);
        }
        if (_state.Project.Groups.Count == 0)
            _groups.Children.Add(Ui.Label("Select fixtures and press + Group."));
    }

    // ---- Profiles --------------------------------------------------------------------------------------

    private void RefreshProfiles()
    {
        var project = _state.Project;
        var profile = project.Profiles.Find(p => p.Id == _profileId) ?? project.Profiles.FirstOrDefault();
        _profileId = profile?.Id;

        var picker = Ui.Combo<FixtureProfile>(project.Profiles, profile!, p =>
        {
            _profileId = p.Id;
            RefreshProfiles();
        }, p => p.DisplayName);
        var buttons = Ui.Row(
            Ui.Button("New", () =>
            {
                var created = new FixtureProfile { Model = "Custom", Channels = [new FixtureChannel { Name = "Dimmer", Role = ChannelRole.Dimmer }] };
                project.Profiles.Add(created);
                _profileId = created.Id;
                _state.Commit();
            }),
            Ui.Button("Duplicate", () =>
            {
                if (profile is null)
                    return;
                var copy = profile.Clone();
                copy.Model += " copy";
                project.Profiles.Add(copy);
                _profileId = copy.Id;
                _state.Commit();
            }),
            Ui.Button("Delete", () =>
            {
                if (profile is null)
                    return;
                if (project.Fixtures.Any(f => f.ProfileId == profile.Id))
                {
                    _profiles.Content = Ui.Column(4, Ui.Label("This profile is in use by patched fixtures; remove them first."), Ui.Button("OK", RefreshProfiles));
                    return;
                }
                project.Profiles.Remove(profile);
                _profileId = null;
                _state.Commit();
            })
        );

        var panel = Ui.Column(4, Ui.Section("Fixture profile"), picker, buttons);
        if (profile is null)
        {
            _profiles.Content = panel;
            return;
        }

        void Edit(Action change)
        {
            change();
            _state.Commit();
        }
        panel.Children.Add(Ui.Field("Manufacturer", Ui.Text(profile.Manufacturer, v => Edit(() => profile.Manufacturer = v))));
        panel.Children.Add(Ui.Field("Model", Ui.Text(profile.Model, v => Edit(() => profile.Model = v))));
        panel.Children.Add(Ui.Field("Mode", Ui.Text(profile.Mode, v => Edit(() => profile.Mode = v))));
        panel.Children.Add(Ui.Field("Look", Ui.Combo(Enum.GetValues<BeamKind>(), profile.Beam, v => Edit(() => profile.Beam = v))));
        panel.Children.Add(Ui.Field("Beam angle °", Ui.Number(profile.BeamAngle, v => Edit(() => profile.BeamAngle = v), 1, 180, 1)));
        if (profile.HasPanTilt)
        {
            panel.Children.Add(Ui.Field("Pan range °", Ui.Number(profile.PanRange, v => Edit(() => profile.PanRange = v), 0, 720, 10)));
            panel.Children.Add(Ui.Field("Tilt range °", Ui.Number(profile.TiltRange, v => Edit(() => profile.TiltRange = v), 0, 360, 10)));
        }

        panel.Children.Add(Ui.Section("Channels"));
        panel.Children.Add(Ui.Label("Lampion fills Dimmer/Red/Green/Blue/White itself; other roles are driven by automation."));
        for (var i = 0; i < profile.Channels.Count; i++)
        {
            var index = i;
            var channel = profile.Channels[i];
            var name = Ui.Text(channel.Name, v => Edit(() => channel.Name = v));
            name.Width = 80;
            var role = Ui.Combo(Enum.GetValues<ChannelRole>(), channel.Role, v => Edit(() => channel.Role = v));
            role.Width = 112;
            var def = Ui.Number(channel.Default, v => Edit(() => channel.Default = (byte)v), 0, 255, 1, "0");
            def.Width = 92;
            ToolTip.SetTip(def, "Value sent when nothing drives this channel");
            var up = Ui.Button("↑", () => Edit(() =>
            {
                if (index == 0)
                    return;
                (profile.Channels[index - 1], profile.Channels[index]) = (profile.Channels[index], profile.Channels[index - 1]);
            }));
            var remove = Ui.Button("✕", () => Edit(() => profile.Channels.RemoveAt(index)));
            panel.Children.Add(Ui.Row(new TextBlock { Text = $"{index + 1}", Width = 18, VerticalAlignment = VerticalAlignment.Center }, name, role, def, up, remove));
        }
        panel.Children.Add(Ui.Button("+ Channel", () => Edit(() => profile.Channels.Add(new FixtureChannel { Name = $"Ch {profile.Channels.Count + 1}" }))));
        var users = project.Fixtures.Count(f => f.ProfileId == profile.Id);
        panel.Children.Add(Ui.Label($"{profile.Channels.Count} channels · used by {users} fixture{(users == 1 ? "" : "s")}"));
        _profiles.Content = panel;
    }

    // ---- Media -----------------------------------------------------------------------------------------

    private Control MediaTab(Func<Task> importAudio, Func<Task> importVideo) =>
        Ui.Column(
            6,
            Ui.Section("Media"),
            Ui.Row(Ui.Button("Import audio…", () => _ = importAudio()), Ui.Button("Import video…", () => _ = importVideo())),
            Ui.Label("Clips land at the playhead. WAV and MP3 get a waveform."),
            _media
        );

    private void RefreshMedia()
    {
        _media.Children.Clear();
        foreach (var track in _state.Project.Tracks.Where(t => t.Kind != TrackKind.Light))
            foreach (var clip in track.Clips.OfType<MediaClip>())
            {
                var missing = !File.Exists(clip.Path);
                var label = new TextBlock
                {
                    Text = $"{(track.Kind == TrackKind.Audio ? "♪" : "▶")} {clip.Name}{(missing ? "  (missing)" : "")}",
                    Foreground = missing ? Brushes.OrangeRed : Brushes.Gainsboro,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                };
                ToolTip.SetTip(label, clip.Path);
                label.PointerPressed += (_, _) => _state.SelectClip(clip.Id);
                _media.Children.Add(label);
            }
    }

    // ---- Effects ---------------------------------------------------------------------------------------

    private Control EffectsTab()
    {
        var list = Ui.Column(3, Ui.Section("Effects"), Ui.Label("Drag onto a light track, or double-click to add at the playhead."));
        foreach (var (kind, name, description) in LightEffect.Catalog)
        {
            var swatch = new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(3), Background = Ui.Brush(LightEffect.Create(kind).SwatchColor) };
            if (kind == "rainbow")
                swatch.Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = { new GradientStop(Colors.Red, 0), new GradientStop(Colors.Lime, 0.5), new GradientStop(Colors.Blue, 1) },
                };
            var item = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2C)),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(8, 5),
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = Ui.Row(swatch, Ui.Column(0, new TextBlock { Text = name, FontWeight = FontWeight.SemiBold }, new TextBlock { Text = description, FontSize = 10.5, Foreground = Brushes.Gray })),
            };
            ToolTip.SetTip(item, description);
            WireEffectDrag(item, kind);
            list.Children.Add(item);
        }
        list.Children.Add(Ui.Section("Transitions"));
        list.Children.Add(Ui.Label("Right-click a light clip → Transition in: Crossfade, Dip to black or Wipe across the fixtures. Drag the clip's top corners for fades."));
        list.Children.Add(Ui.Section("Automation"));
        list.Children.Add(Ui.Label("Right-click a clip → Add automation. Hue shift 0→1 turns any colour into a rainbow; Pan/Tilt/Strobe lanes drive those channels on every fixture that has them."));
        return list;
    }

    private void WireEffectDrag(Border item, string kind)
    {
        var dragging = false;
        item.PointerPressed += (_, e) =>
        {
            if (e.ClickCount == 2)
            {
                var track = _state.SelectedTrack is { Kind: TrackKind.Light } t ? t : _state.Project.Tracks.FirstOrDefault(x => x.Kind == TrackKind.Light) ?? TimelineOps.AddLightTrack(_state);
                TimelineOps.AddEffectClip(_state, track, kind, _state.Playhead);
                return;
            }
            dragging = true;
            e.Pointer.Capture(item);
        };
        item.PointerMoved += (_, e) =>
        {
            if (dragging)
                _timeline.PreviewDrop(e.GetPosition(_timeline));
        };
        item.PointerReleased += (_, e) =>
        {
            if (!dragging)
                return;
            dragging = false;
            e.Pointer.Capture(null);
            _timeline.Drop(kind, e.GetPosition(_timeline));
        };
    }
}
