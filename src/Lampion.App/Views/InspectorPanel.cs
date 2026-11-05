using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lampion.App.Editor;
using Lampion.Core.Model;
using LightEffect = Lampion.Core.Model.Effect;

namespace Lampion.App.Views;

/// <summary>
/// Context-sensitive properties for whatever is selected — the After Effects "Effect Controls" panel.
/// Effect parameters are discovered by reflection, so a new effect class gets its controls for free.
/// </summary>
public sealed class InspectorPanel : ScrollViewer
{
    private readonly EditorState _state;
    private readonly StackPanel _root = new() { Spacing = 3, Margin = new Thickness(10, 6, 10, 16) };
    private Action? _onPlayhead;
    private bool _selfEdit;

    protected override Type StyleKeyOverride => typeof(ScrollViewer);

    public InspectorPanel(EditorState state)
    {
        _state = state;
        Content = _root;
        HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;
        state.SelectionChanged += Rebuild;
        state.ProjectReplaced += Rebuild;
        state.Changed += () =>
        {
            if (!_selfEdit && !IsKeyboardFocusWithin)
                Rebuild();
        };
        state.PlayheadChanged += () => _onPlayhead?.Invoke();
        Rebuild();
    }

    /// <summary>Applies an edit made here without rebuilding the panel under the user's pointer.</summary>
    private void Edit(Action change, bool commit = true)
    {
        _selfEdit = true;
        try
        {
            change();
            if (commit)
                _state.Commit();
            else
                _state.Touch();
        }
        finally
        {
            _selfEdit = false;
        }
    }

    private void Rebuild()
    {
        _root.Children.Clear();
        _onPlayhead = null;
        switch (_state.SelectionKind)
        {
            case SelectionKind.Clip when _state.PrimaryClip is LightClip light:
                BuildLightClip(light);
                break;
            case SelectionKind.Clip when _state.PrimaryClip is MediaClip media:
                BuildMediaClip(media);
                break;
            case SelectionKind.Track when _state.SelectedTrack is { } track:
                BuildTrack(track);
                break;
            case SelectionKind.Fixture when _state.PrimaryFixture is { } fixture:
                BuildFixtures(fixture);
                break;
            case SelectionKind.StageObject when _state.SelectedObject is { } obj:
                BuildObject(obj);
                break;
            default:
                BuildProject();
                break;
        }
    }

    private void Header(string title, string? subtitle = null)
    {
        _root.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 4, 0, 0) });
        if (subtitle is not null)
            _root.Children.Add(new TextBlock { Text = subtitle, Foreground = Brushes.Gray, FontSize = 11, TextWrapping = TextWrapping.Wrap });
    }

    private void Add(Control control) => _root.Children.Add(control);

    private void Field(string label, Control editor) => Add(Ui.Field(label, editor));

    private NumericUpDown Num(double value, Action<double> set, double min, double max, double step, string format = "0.###", bool commit = true) =>
        Ui.Number(value, v => Edit(() => set(v), commit), min, max, step, format);

    // ---- Light clip ------------------------------------------------------------------------------------

    private void BuildLightClip(LightClip clip)
    {
        var count = _state.SelectedClipIds.Count;
        Header(clip.Name, count > 1 ? $"{count} clips selected — editing the first" : "Light clip");
        Add(Ui.Section("Clip"));
        Field("Name", Ui.Text(clip.Name, v => Edit(() => clip.Name = v)));
        Field("Start (s)", Num(clip.Start, v => clip.Start = Math.Max(0, v), 0, 36000, 0.1));
        Field("Duration (s)", Num(clip.Duration, v => clip.Duration = Math.Max(0.05, v), 0.05, 36000, 0.1));
        Field("Fade in (s)", Num(clip.FadeIn, v => clip.FadeIn = v, 0, 3600, 0.1));
        Field("Fade out (s)", Num(clip.FadeOut, v => clip.FadeOut = v, 0, 3600, 0.1));
        Field("Fade curve", Ui.Combo(Enum.GetValues<Easing>(), clip.FadeEasing, v => Edit(() => clip.FadeEasing = v)));

        Add(Ui.Section("Effect"));
        Field(
            "Type",
            Ui.Combo(
                LightEffect.Catalog.Select(c => c.Kind),
                LightEffect.Catalog.First(c => c.Name == clip.Effect.DisplayName).Kind,
                kind =>
                {
                    var color = clip.Effect.SwatchColor;
                    Edit(() =>
                    {
                        var replacement = LightEffect.Create(kind);
                        replacement.Intensity = clip.Effect.Intensity;
                        // Keep the colour the user picked when switching between single-colour effects.
                        replacement.GetType().GetProperty("Color")?.SetValue(replacement, color);
                        if (clip.Name == clip.Effect.DisplayName)
                            clip.Name = replacement.DisplayName;
                        clip.Effect = replacement;
                    });
                    Rebuild();
                },
                kind => LightEffect.Catalog.First(c => c.Kind == kind).Name
            )
        );
        EffectEditors(clip.Effect);

        Add(Ui.Section("Transition in"));
        var transitionKinds = new List<TransitionKind?> { null };
        transitionKinds.AddRange(Enum.GetValues<TransitionKind>().Select(k => (TransitionKind?)k));
        Field(
            "Kind",
            Ui.Combo(transitionKinds, clip.TransitionIn?.Kind, k =>
            {
                Edit(() =>
                {
                    if (k is null)
                        clip.TransitionIn = null;
                    else if (clip.TransitionIn is { } t)
                        t.Kind = k.Value;
                    else
                        clip.TransitionIn = new Transition { Kind = k.Value, Duration = Math.Min(clip.Duration, _state.SecondsPerBeat * 2) };
                });
                Rebuild();
            }, k => k?.ToString() ?? "Cut (none)")
        );
        if (clip.TransitionIn is { } transition)
        {
            Field("Duration (s)", Num(transition.Duration, v => transition.Duration = v, 0.05, 60, 0.1));
            Field("Curve", Ui.Combo(Enum.GetValues<Easing>(), transition.Easing, v => Edit(() => transition.Easing = v)));
            Add(Ui.Label("Centred on the cut; blends from the clip that ends where this one starts."));
        }

        BuildAutomation(clip);
    }

    private void EffectEditors(LightEffect effect)
    {
        foreach (var prop in effect.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanWrite || prop.GetCustomAttribute<System.Text.Json.Serialization.JsonIgnoreAttribute>() is not null)
                continue;
            var label = Humanize(prop.Name);
            var value = prop.GetValue(effect);
            switch (value)
            {
                case double d:
                    var (min, max, step) = RangeFor(prop.Name);
                    var slider = Ui.Slider(d, min, max, v => Edit(() => prop.SetValue(effect, Math.Round(v, 3)), commit: false), () => _state.Commit());
                    var box = Num(d, v => prop.SetValue(effect, v), min, max, step);
                    box.Width = 96;
                    slider.ValueChanged += (_, e) => box.Value = (decimal)Math.Round(e.NewValue, 3);
                    var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                    grid.Children.Add(slider);
                    Grid.SetColumn(box, 1);
                    grid.Children.Add(box);
                    Field(label, grid);
                    break;
                case int i:
                    Field(label, Num(i, v => prop.SetValue(effect, (int)v), 0, 99999, 1, "0"));
                    break;
                case string s when s.StartsWith('#'):
                    Field(label, Ui.ColorPicker(s, hex => Edit(() => prop.SetValue(effect, hex), commit: false), () => _state.Commit()));
                    break;
                case List<string> colors:
                    Field(label, ColorList(colors));
                    break;
                case Enum e:
                    var values = Enum.GetValues(e.GetType()).Cast<object>().ToList();
                    Field(label, Ui.Combo(values, value, v => Edit(() => prop.SetValue(effect, v))));
                    break;
            }
        }
    }

    private Control ColorList(List<string> colors)
    {
        var panel = new WrapPanel();
        for (var i = 0; i < colors.Count; i++)
        {
            var index = i;
            var picker = Ui.ColorPicker(colors[i], hex => Edit(() => colors[index] = hex, commit: false), () => _state.Commit());
            picker.ContextMenu = new ContextMenu { Items = { new MenuItem { Header = "Remove" } } };
            ((MenuItem)picker.ContextMenu.Items[0]!).Click += (_, _) =>
            {
                if (colors.Count <= 1)
                    return;
                Edit(() => colors.RemoveAt(index));
                Rebuild();
            };
            panel.Children.Add(picker);
        }
        panel.Children.Add(Ui.Button("+", () =>
        {
            Edit(() => colors.Add(colors.LastOrDefault() ?? "#FFFFFF"));
            Rebuild();
        }, "Add a colour (right-click a colour to remove it)"));
        return panel;
    }

    private static (double Min, double Max, double Step) RangeFor(string name) =>
        name switch
        {
            "Speed" => (-4, 4, 0.05),
            "Spread" => (0, 3, 0.05),
            "Rate" => (0.05, 30, 0.1),
            "StepTime" => (0.02, 8, 0.05),
            "Tail" => (0, 12, 0.25),
            "Decay" => (0.02, 3, 0.05),
            "Offset" => (0, 8, 0.05),
            "Phase" or "FixtureOffset" => (-1, 1, 0.05),
            _ => (0, 1, 0.01),
        };

    private static string Humanize(string name) => string.Concat(name.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + char.ToLowerInvariant(c) : c.ToString()));

    private void BuildAutomation(LightClip clip)
    {
        Add(Ui.Section("Automation"));
        Add(Ui.Label("Keyframes are relative to the clip. Expand the track (▸) to drag them on the timeline."));
        foreach (var lane in clip.Automation.ToList())
        {
            var now = new TextBlock { Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center, Width = 60 };
            void ShowNow()
            {
                var local = _state.Playhead - clip.Start;
                now.Text = local >= 0 && local <= clip.Duration ? $"= {lane.Evaluate(local):0.###}" : "";
            }
            ShowNow();
            _onPlayhead += ShowNow;

            var enabled = new CheckBox { IsChecked = lane.Enabled, Content = lane.Label, FontWeight = FontWeight.SemiBold };
            enabled.IsCheckedChanged += (_, _) => Edit(() => lane.Enabled = enabled.IsChecked == true);
            var key = Ui.Button("◆+", () =>
            {
                var local = Math.Clamp(_state.Playhead - clip.Start, 0, clip.Duration);
                Edit(() =>
                {
                    lane.Keys.RemoveAll(k => Math.Abs(k.Time - local) < 1e-3);
                    lane.Keys.Add(new Keyframe { Time = local, Value = lane.Evaluate(local) });
                    lane.SortKeys();
                });
                Rebuild();
            }, "Add a keyframe at the playhead");
            var remove = Ui.Button("✕", () =>
            {
                Edit(() => clip.Automation.Remove(lane));
                Rebuild();
            }, "Remove this lane");
            Add(Ui.Row(enabled, now, key, remove));

            var keys = new StackPanel { Spacing = 1, Margin = new Thickness(18, 0, 0, 4) };
            foreach (var k in lane.Keys)
            {
                var time = Num(k.Time, v =>
                {
                    k.Time = Math.Clamp(v, 0, clip.Duration);
                    lane.SortKeys();
                }, 0, clip.Duration, 0.05);
                time.Width = 100;
                var (min, max) = lane.Target == AutomationTarget.HueShift ? (-20.0, 20.0) : (0.0, 1.0);
                var val = Num(k.Value, v => k.Value = v, min, max, lane.Target == AutomationTarget.HueShift ? 0.05 : 0.01);
                val.Width = 100;
                var easing = Ui.Combo(Enum.GetValues<Easing>(), k.Easing, v => Edit(() => k.Easing = v));
                easing.Width = 104;
                var del = Ui.Button("✕", () =>
                {
                    Edit(() => lane.Keys.Remove(k));
                    Rebuild();
                });
                var isSelected = _state.SelectedKey is { } sk && sk.Key == k;
                var row = Ui.Row(new TextBlock { Text = isSelected ? "◆" : "◇", VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Goldenrod }, time, val, easing, del);
                ToolTip.SetTip(row, "time (s) · value · curve to the next key");
                keys.Children.Add(row);
            }
            Add(keys);
        }

        var target = AutomationTarget.HueShift;
        var role = ChannelRole.Pan;
        var channel = 1;
        var roleCombo = Ui.Combo(Enum.GetValues<ChannelRole>(), role, v => role = v);
        var channelBox = Ui.Number(1, v => channel = (int)v, 1, 512, 1, "0");
        roleCombo.IsVisible = false;
        channelBox.IsVisible = false;
        var targetCombo = Ui.Combo(Enum.GetValues<AutomationTarget>(), target, v =>
        {
            target = v;
            roleCombo.IsVisible = v == AutomationTarget.Role;
            channelBox.IsVisible = v == AutomationTarget.Channel;
        }, t => t switch
        {
            AutomationTarget.HueShift => "Hue shift (rainbow)",
            AutomationTarget.Role => "Channel by role…",
            AutomationTarget.Channel => "Channel by number…",
            _ => t.ToString(),
        });
        Add(Ui.Field("New lane", targetCombo));
        Add(Ui.Field("", roleCombo));
        Add(Ui.Field("", channelBox));
        Add(Ui.Button("+ Add lane", () =>
        {
            _selfEdit = true;
            TimelineOps.AddLane(_state, clip, target, role, channel);
            _selfEdit = false;
            if (_state.FindClip(clip.Id) is { } found)
                found.Track.Expanded = true;
            _state.Touch();
            Rebuild();
        }));
        Add(Ui.Label("Colour targets are mapped to each fixture's R/G/B/W/dimmer channels automatically; role and channel lanes write that channel directly."));
    }

    // ---- Media clip ------------------------------------------------------------------------------------

    private void BuildMediaClip(MediaClip clip)
    {
        Header(clip.Name, clip.Path);
        Field("Name", Ui.Text(clip.Name, v => Edit(() => clip.Name = v)));
        Field("Start (s)", Num(clip.Start, v => clip.Start = Math.Max(0, v), 0, 36000, 0.1));
        Field("Duration (s)", Num(clip.Duration, v => clip.Duration = Math.Max(0.05, v), 0.05, 36000, 0.1));
        Field("In point (s)", Num(clip.MediaOffset, v => clip.MediaOffset = Math.Max(0, v), 0, 36000, 0.1));
        Field("Volume", Num(clip.Volume, v => clip.Volume = v, 0, 2, 0.05));
        if (clip.SourceDuration is { } source)
            Add(Ui.Label($"Source length {source:0.00} s"));
        Add(Ui.Label("Line the music up with the beat grid: set BPM and Offset in the transport bar."));
    }

    // ---- Track -----------------------------------------------------------------------------------------

    private void BuildTrack(Track track)
    {
        Header(track.Name, $"{track.Kind} track");
        Field("Name", Ui.Text(track.Name, v => Edit(() => track.Name = v)));
        if (track.Kind != TrackKind.Light)
        {
            Field("Muted", Toggle(track.Muted, v => track.Muted = v));
            return;
        }
        Field("Blend", Ui.Combo(Enum.GetValues<BlendMode>(), track.Blend, v => Edit(() => track.Blend = v)));
        Field("Fixture order", Ui.Combo(Enum.GetValues<FixtureOrder>(), track.Order, v => Edit(() => track.Order = v)));
        Add(Ui.Label("Upper tracks draw over lower ones. Order sets how chases, rainbow spread and wipes walk the fixtures."));

        Add(Ui.Section("Groups"));
        foreach (var group in _state.Project.Groups)
        {
            var box = new CheckBox { Content = $"{group.Name} ({group.FixtureIds.Count})", IsChecked = track.GroupIds.Contains(group.Id) };
            box.IsCheckedChanged += (_, _) => Edit(() =>
            {
                track.GroupIds.Remove(group.Id);
                if (box.IsChecked == true)
                    track.GroupIds.Add(group.Id);
            });
            Add(box);
        }
        Add(Ui.Section("Fixtures"));
        Add(Ui.Button("Use selected fixtures from the stage", () =>
        {
            var lastSelection = _lastFixtureSelection;
            Edit(() => track.FixtureIds = [.. lastSelection]);
            Rebuild();
        }, "Select fixtures in the 3D view or list first, then select this track"));
        var list = new WrapPanel();
        foreach (var fixture in _state.Project.Fixtures)
        {
            var box = new CheckBox { Content = fixture.Name, IsChecked = track.FixtureIds.Contains(fixture.Id), Margin = new Thickness(0, 0, 8, 0) };
            box.IsCheckedChanged += (_, _) => Edit(() =>
            {
                track.FixtureIds.Remove(fixture.Id);
                if (box.IsChecked == true)
                    track.FixtureIds.Add(fixture.Id);
            });
            list.Children.Add(box);
        }
        Add(list);
    }

    private HashSet<string> _lastFixtureSelection = [];

    private CheckBox Toggle(bool value, Action<bool> set)
    {
        var box = new CheckBox { IsChecked = value };
        box.IsCheckedChanged += (_, _) => Edit(() => set(box.IsChecked == true));
        return box;
    }

    // ---- Fixtures --------------------------------------------------------------------------------------

    private void BuildFixtures(Fixture fixture)
    {
        _lastFixtureSelection = [.. _state.SelectedFixtureIds];
        var project = _state.Project;
        var selected = _state.SelectedFixtureIds.Select(project.FixtureById).OfType<Fixture>().ToList();
        var profile = project.ProfileOf(fixture);
        Header(selected.Count > 1 ? $"{selected.Count} fixtures" : fixture.Name, profile?.DisplayName);

        if (selected.Count == 1)
        {
            Add(Ui.Section("Patch"));
            Field("Name", Ui.Text(fixture.Name, v => Edit(() => fixture.Name = v)));
            Field("Profile", Ui.Combo<FixtureProfile>(project.Profiles, profile!, p => Edit(() => fixture.ProfileId = p.Id), p => p.DisplayName));
            Field("Universe", Num(fixture.Universe, v => fixture.Universe = (int)v, 1, 64, 1, "0"));
            Field("Address", Num(fixture.Address, v => fixture.Address = (int)v, 1, 512, 1, "0"));
            Add(Ui.Button("Next free address", () =>
            {
                var free = project.NextFreeAddress(fixture.Universe, profile?.Channels.Count ?? 1, fixture.Id);
                if (free > 0)
                    Edit(() => fixture.Address = free);
                Rebuild();
            }));
            if (profile?.Has(ChannelRole.White) == true)
                Field("Auto white", Toggle(fixture.ExtractWhite, v => fixture.ExtractWhite = v));
            if (profile is { HasColor: false })
                Field("Gel", Ui.ColorPicker(fixture.GelColor, hex => Edit(() => fixture.GelColor = hex, commit: false), () => _state.Commit()));
        }

        Add(Ui.Section("Placement (metres)"));
        Add(Ui.Label("X across the stage (audience right +), Y up from the floor, Z toward the audience."));
        if (selected.Count == 1)
        {
            Field("X", Num(fixture.Position.X, v => fixture.Position = fixture.Position with { X = v }, -200, 200, 0.05));
            Field("Y (height)", Num(fixture.Position.Y, v => fixture.Position = fixture.Position with { Y = v }, -10, 100, 0.05));
            Field("Z", Num(fixture.Position.Z, v => fixture.Position = fixture.Position with { Z = v }, -200, 200, 0.05));
        }
        else
        {
            Add(Ui.Label("Height for all selected:"));
            Field("Y (height)", Num(fixture.Position.Y, v => selected.ForEach(f => f.Position = f.Position with { Y = v }), -10, 100, 0.05));
        }
        Field("Yaw °", Num(fixture.Yaw, v => selected.ForEach(f => f.Yaw = v), -360, 360, 5, "0.#"));
        Field("Pitch °", Num(fixture.Pitch, v => selected.ForEach(f => f.Pitch = v), -180, 90, 5, "0.#"));

        Add(Ui.Section("Aim at a point"));
        double ax = 0, ay = 1, az = 0;
        var aimRow = Ui.Row(Ui.Number(ax, v => ax = v, -100, 100, 0.25), Ui.Number(ay, v => ay = v, -10, 50, 0.25), Ui.Number(az, v => az = v, -100, 100, 0.25));
        foreach (var c in aimRow.Children)
            ((NumericUpDown)c).Width = 96;
        Add(aimRow);
        Add(Ui.Button("Aim selected here", () =>
        {
            Edit(() => selected.ForEach(f => StageMath.AimAt(f, new Vec3(ax, ay, az))));
            Rebuild();
        }, "Or pick the Aim tool in the stage view and click a spot"));

        var trusses = project.Stage.Objects.Where(o => o.Kind == StageObjectKind.Truss).ToList();
        if (trusses.Count > 0)
        {
            Add(Ui.Section("Hang on truss"));
            var truss = trusses[0];
            double drop = 0.35;
            Field("Truss", Ui.Combo(trusses, truss, t => truss = t, t => $"{t.Name} ({t.Size.X:0.##} m)"));
            Field("Drop (m)", Ui.Number(drop, v => drop = v, 0, 3, 0.05));
            Add(Ui.Button($"Distribute {selected.Count} evenly", () =>
            {
                Edit(() => StageMath.DistributeAlong(truss, selected.OrderBy(f => f.Position.X).ToList(), drop));
                Rebuild();
            }, "Spread the selected fixtures along the truss, centred, hung below it"));
        }

        if (selected.Count == 1 && profile is not null)
        {
            Add(Ui.Section("Live DMX"));
            var values = new TextBlock { FontFamily = new FontFamily("Consolas, Menlo, monospace"), FontSize = 11, Foreground = Brushes.Gainsboro };
            void ShowValues()
            {
                var slice = _state.CurrentFrame.Slice(fixture, profile.Channels.Count).ToArray();
                values.Text = string.Join(
                    "\n",
                    profile.Channels.Select((c, i) => $"{fixture.Address + i,3}  {c.Name,-12} {c.Role,-11} {(i < slice.Length ? slice[i] : 0),3}")
                );
            }
            ShowValues();
            _onPlayhead += ShowValues;
            Add(values);
        }
    }

    // ---- Stage object ----------------------------------------------------------------------------------

    private void BuildObject(StageObject obj)
    {
        Header(obj.Name, $"{obj.Kind} — all sizes in metres");
        Field("Name", Ui.Text(obj.Name, v => Edit(() => obj.Name = v)));
        Field("Kind", Ui.Combo(Enum.GetValues<StageObjectKind>(), obj.Kind, v => Edit(() => obj.Kind = v)));
        Add(Ui.Section("Position (centre)"));
        Field("X", Num(obj.Position.X, v => obj.Position = obj.Position with { X = v }, -200, 200, 0.05));
        Field("Y", Num(obj.Position.Y, v => obj.Position = obj.Position with { Y = v }, -10, 100, 0.05));
        Field("Z", Num(obj.Position.Z, v => obj.Position = obj.Position with { Z = v }, -200, 200, 0.05));
        Add(Ui.Section("Size"));
        Field(obj.Kind == StageObjectKind.Truss ? "Length" : "Width", Num(obj.Size.X, v => obj.Size = obj.Size with { X = Math.Max(0.05, v) }, 0.05, 200, 0.05));
        Field("Height", Num(obj.Size.Y, v => obj.Size = obj.Size with { Y = Math.Max(0.02, v) }, 0.02, 100, 0.05));
        Field("Depth", Num(obj.Size.Z, v => obj.Size = obj.Size with { Z = Math.Max(0.02, v) }, 0.02, 200, 0.05));
        Field("Yaw °", Num(obj.Yaw, v => obj.Yaw = v, -360, 360, 5, "0.#"));
        Field("Colour", Ui.ColorPicker(obj.Color, hex => Edit(() => obj.Color = hex, commit: false), () => _state.Commit()));
        if (obj.Kind == StageObjectKind.Platform)
        {
            Add(Ui.Button("Sit on the floor", () =>
            {
                Edit(() => obj.Position = obj.Position with { Y = obj.Size.Y / 2 });
                Rebuild();
            }));
            Add(Ui.Label($"Deck surface at {obj.Position.Y + obj.Size.Y / 2:0.00} m"));
        }
        if (obj.Kind == StageObjectKind.Screen)
            Add(Ui.Label("Plays the timeline's video track in the preview."));
        Add(Ui.Button("Duplicate", () =>
        {
            var copy = obj.Clone();
            copy.Name += " copy";
            copy.Position += new Vec3(0.5, 0, 0);
            _state.Project.Stage.Objects.Add(copy);
            _state.Commit();
            _state.SelectObject(copy.Id);
        }));
    }

    // ---- Nothing selected: project and stage ----------------------------------------------------------

    private void BuildProject()
    {
        var project = _state.Project;
        Header(project.Name, "Project");
        Field("Name", Ui.Text(project.Name, v => Edit(() => project.Name = v)));
        Add(Ui.Section("Stage look"));
        Field("Haze", Ui.Slider(project.Stage.Haze, 0, 1, v => Edit(() => project.Stage.Haze = v, commit: false), () => _state.Commit()));
        Field("House light", Ui.Slider(project.Stage.Ambient, 0, 1, v => Edit(() => project.Stage.Ambient = v, commit: false), () => _state.Commit()));
        Field("Light bounce", Toggle(project.Stage.LightBounce, v => project.Stage.LightBounce = v));
        Add(Ui.Section("Summary"));
        var universes = project.Fixtures.Select(f => f.Universe).Distinct().Count();
        Add(Ui.Label($"{project.Fixtures.Count} fixtures in {universes} universe(s) · {project.Tracks.Count} tracks · {project.AllClips.Count()} clips"));
        Add(Ui.Section("Shortcuts"));
        Add(
            new TextBlock
            {
                Text = string.Join(
                    "\n",
                    "Space  play / pause          Home  to start",
                    "Ctrl+K  split at playhead    Ctrl+D  duplicate",
                    "Ctrl+C / Ctrl+V  copy / paste clip",
                    "Del  delete selection        M  add marker",
                    "Ctrl+Z / Ctrl+Y  undo / redo   Ctrl+S  save",
                    "= / -  zoom timeline         \\  zoom to fit",
                    "1-4  stage views   W move   A aim   F frame",
                    "Timeline: double-click empty track = new clip,",
                    "drag top corners = fades, edges = trim,",
                    "Alt while dragging = no snapping,",
                    "Ctrl+wheel = zoom, Shift+wheel = scroll.",
                    "Stage: right-drag orbit, middle-drag pan,",
                    "wheel zoom, Alt+drag = change height."
                ),
                FontFamily = new FontFamily("Consolas, Menlo, monospace"),
                FontSize = 11,
                Foreground = Brushes.Gray,
            }
        );
    }
}
