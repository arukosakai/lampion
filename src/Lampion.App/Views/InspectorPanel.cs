using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lampion.App.Controls;
using Lampion.App.Editor;
using Lampion.Core.Model;
using LightEffect = Lampion.Core.Model.Effect;

namespace Lampion.App.Views;

/// <summary>
/// Effect Controls, modelled on After Effects: twirl-down sections, blue values you scrub or type, and a
/// stopwatch on every animatable property. With the stopwatch on, changing a value at the playhead sets a
/// keyframe there; ◀ ◆ ▶ step between keys.
/// </summary>
public sealed class InspectorPanel : ScrollViewer
{
    private readonly EditorState _state;
    private readonly StackPanel _root = new() { Spacing = 0, Margin = new Thickness(6, 4, 8, 16) };
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

    /// <summary>Opens the project settings; the main window supplies it so this panel needs no window of its own.</summary>
    public Action? OpenProjectSettings { get; set; }

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
                BuildEmpty();
                break;
        }
    }

    // ---- Building blocks -------------------------------------------------------------------------------

    private void Title(string title, string? subtitle)
    {
        _root.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeight.SemiBold, Margin = new Thickness(4, 2, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis });
        _root.Children.Add(new TextBlock { Text = subtitle ?? "", Foreground = Brushes.Gray, FontSize = 11, Margin = new Thickness(4, 0, 0, 8) });
    }

    private TwirlSection Section(string key, string title, bool open = true, params Control[] trailing)
    {
        var section = TwirlSection.Titled(key, title, open, trailing);
        _root.Children.Add(section);
        return section;
    }

    private static void Row(TwirlSection section, string name, Control value, string? tip = null) =>
        section.Body.Children.Add(new PropertyRow(name, value, tooltip: tip));

    /// <summary>A static number (no stopwatch) that scrubs live and commits on release.</summary>
    private ScrubValue Scrub(double value, Action<double> set, double min, double max, double step, double scale = 1, string format = "0.##", string unit = "")
    {
        var scrub = new ScrubValue(value, min, max, step, scale, format, unit);
        scrub.Changing += v => Edit(() => set(v), commit: false);
        scrub.Committed += v => Edit(() => set(v));
        return scrub;
    }

    private static Control Compact(Control control)
    {
        control.HorizontalAlignment = HorizontalAlignment.Left;
        control.MinWidth = 130;
        if (control is ComboBox combo)
            combo.MinHeight = 22;
        return control;
    }

    private Control TextField(string value, Action<string> set)
    {
        var box = Ui.Text(value, v => Edit(() => set(v)));
        box.MinHeight = 22;
        box.Padding = new Thickness(4, 1);
        return box;
    }

    // ---- Light clip ------------------------------------------------------------------------------------

    private void BuildLightClip(LightClip clip)
    {
        var count = _state.SelectedClipIds.Count;
        var track = _state.FindClip(clip.Id)?.Track;
        Title(
            $"{track?.Name ?? "Track"} • {clip.Name}",
            count > 1 ? $"{count} clips selected — showing the first" : $"{Time(clip.Start)} – {Time(clip.End)}"
        );

        var clipSection = Section("clip", "Clip");
        Row(clipSection, "Name", TextField(clip.Name, v => clip.Name = v));
        Row(clipSection, "Start", Scrub(clip.Start, v => clip.Start = Math.Max(0, v), 0, 36000, 0.02, unit: " s"));
        Row(clipSection, "Duration", Scrub(clip.Duration, v => clip.Duration = Math.Max(0.05, v), 0.05, 36000, 0.02, unit: " s"));
        Row(clipSection, "Fade in", Scrub(clip.FadeIn, v => clip.FadeIn = Math.Clamp(v, 0, clip.Duration), 0, 3600, 0.02, unit: " s"));
        Row(clipSection, "Fade out", Scrub(clip.FadeOut, v => clip.FadeOut = Math.Clamp(v, 0, clip.Duration), 0, 3600, 0.02, unit: " s"));
        Row(clipSection, "Fade curve", Compact(Ui.Combo(Enum.GetValues<Easing>(), clip.FadeEasing, v => Edit(() => clip.FadeEasing = v))));

        var light = Section("light", "Light");
        light.Body.Children.Add(LaneProperty(clip, AutomationTarget.Opacity, "Opacity", 0, 1, 0.005, 100, "0", "%"));
        light.Body.Children.Add(LaneProperty(clip, AutomationTarget.Intensity, "Intensity", 0, 4, 0.005, 100, "0", "%"));
        light.Body.Children.Add(LaneProperty(clip, AutomationTarget.HueShift, "Hue shift", -100, 100, 1 / 360.0, 360, "0", "°", tip: "Animate 0° → 360° for a rainbow; every full turn is another trip round the wheel"));
        light.Body.Children.Add(LaneProperty(clip, AutomationTarget.Saturation, "Saturation", 0, 2, 0.005, 100, "0", "%"));
        light.Body.Children.Add(LaneProperty(clip, AutomationTarget.White, "White", 0, 1, 0.005, 100, "0", "%", tip: "Drives the white LED of fixtures that have one"));

        BuildEffect(clip);
        BuildTransition(clip);
        BuildChannels(clip);
    }

    private void BuildEffect(LightClip clip)
    {
        var effect = clip.Effect;
        var change = new MenuFlyout();
        foreach (var (kind, name, _) in LightEffect.Catalog)
        {
            var item = new MenuItem { Header = name };
            item.Click += (_, _) => ReplaceEffect(clip, LightEffect.Create(kind), keepColor: true);
            change.Items.Add(item);
        }
        var changeButton = new GlyphButton("⇄", "Change effect", () => { });
        changeButton.Flyout = change;
        var reset = new GlyphButton("Reset", "Reset the effect's parameters", () => ReplaceEffect(clip, LightEffect.Create(KindOf(effect)), keepColor: false));
        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                new Border { Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x40)), CornerRadius = new CornerRadius(3), Padding = new Thickness(4, 0), Child = new TextBlock { Text = "fx", FontStyle = FontStyle.Italic, FontSize = 11 } },
                new TextBlock { Text = effect.DisplayName, FontWeight = FontWeight.SemiBold },
            },
        };
        var section = new TwirlSection("effect", header, true, changeButton, reset);
        _root.Children.Add(section);

        foreach (var prop in effect.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanWrite || prop.GetCustomAttribute<System.Text.Json.Serialization.JsonIgnoreAttribute>() is not null)
                continue;
            var label = Humanize(prop.Name);
            switch (prop.GetValue(effect))
            {
                case double d:
                    var (min, max, step, scale, unit) = RangeFor(prop.Name);
                    Row(section, label, Scrub(d, v => prop.SetValue(effect, Math.Round(v, 4)), min, max, step, scale, scale == 100 ? "0" : "0.##", unit));
                    break;
                case int i:
                    Row(section, label, Scrub(i, v => prop.SetValue(effect, (int)Math.Round(v)), 0, 99999, 0.5, format: "0"));
                    break;
                case string s when s.StartsWith('#'):
                    Row(section, label, Ui.ColorPicker(s, hex => Edit(() => prop.SetValue(effect, hex), commit: false), () => _state.Commit()));
                    break;
                case List<string> colors:
                    Row(section, label, ColorList(colors));
                    break;
                case Enum e:
                    Row(section, label, Compact(Ui.Combo(Enum.GetValues(e.GetType()).Cast<object>().ToList(), e, v => Edit(() => prop.SetValue(effect, v)))));
                    break;
            }
        }
    }

    private void ReplaceEffect(LightClip clip, LightEffect replacement, bool keepColor)
    {
        var old = clip.Effect;
        Edit(() =>
        {
            if (keepColor)
            {
                replacement.Intensity = old.Intensity;
                // Switching between single-colour effects keeps the colour the user picked.
                replacement.GetType().GetProperty("Color")?.SetValue(replacement, old.SwatchColor);
            }
            if (clip.Name == old.DisplayName)
                clip.Name = replacement.DisplayName;
            clip.Effect = replacement;
        });
        Rebuild();
    }

    private static string KindOf(LightEffect effect) => LightEffect.Catalog.First(c => c.Name == effect.DisplayName).Kind;

    private Control ColorList(List<string> colors)
    {
        var panel = new WrapPanel();
        for (var i = 0; i < colors.Count; i++)
        {
            var index = i;
            var picker = Ui.ColorPicker(colors[i], hex => Edit(() => colors[index] = hex, commit: false), () => _state.Commit());
            var remove = new MenuItem { Header = "Remove colour" };
            remove.Click += (_, _) =>
            {
                if (colors.Count <= 1)
                    return;
                Edit(() => colors.RemoveAt(index));
                Rebuild();
            };
            picker.ContextMenu = new ContextMenu { Items = { remove } };
            panel.Children.Add(picker);
        }
        panel.Children.Add(new GlyphButton("+", "Add a colour (right-click one to remove it)", () =>
        {
            Edit(() => colors.Add(colors.LastOrDefault() ?? "#FFFFFF"));
            Rebuild();
        }));
        return panel;
    }

    private static (double Min, double Max, double Step, double Scale, string Unit) RangeFor(string name) =>
        name switch
        {
            "Intensity" or "Saturation" or "Duty" or "Minimum" or "Background" or "FadeRatio" => (0, 1, 0.005, 100, "%"),
            "Spread" => (0, 4, 0.01, 1, "×"),
            "StartHue" => (0, 1, 1 / 360.0, 360, "°"),
            "Speed" => (-4, 4, 0.005, 1, " /s"),
            "Rate" => (0.05, 30, 0.02, 1, " Hz"),
            "StepTime" or "Decay" or "Offset" => (0, 8, 0.005, 1, " s"),
            "Tail" => (0, 12, 0.02, 1, " fx"),
            "Phase" or "FixtureOffset" => (-1, 1, 0.005, 1, ""),
            _ => (0, 1, 0.005, 1, ""),
        };

    private static string Humanize(string name) => string.Concat(name.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + char.ToLowerInvariant(c) : c.ToString()));

    private void BuildTransition(LightClip clip)
    {
        var transitionKinds = new List<TransitionKind?> { null };
        transitionKinds.AddRange(Enum.GetValues<TransitionKind>().Select(k => (TransitionKind?)k));
        var section = Section("transition", clip.TransitionIn is { } t ? $"Transition in · {Spaced(t.Kind)}" : "Transition in", open: clip.TransitionIn is not null);
        Row(section, "Type", Compact(Ui.Combo(transitionKinds, clip.TransitionIn?.Kind, k =>
        {
            Edit(() =>
            {
                if (k is null)
                    clip.TransitionIn = null;
                else if (clip.TransitionIn is { } existing)
                    existing.Kind = k.Value;
                else
                    clip.TransitionIn = new Transition { Kind = k.Value, Duration = Math.Min(clip.Duration, _state.SecondsPerBeat * 2) };
            });
            Rebuild();
        }, k => k is { } kind ? Spaced(kind) : "Cut (none)")), "Blends from the clip ending where this one starts. Drag one from the Effects panel onto a clip too.");
        if (clip.TransitionIn is not { } transition)
            return;
        Row(section, "Duration", Scrub(transition.Duration, v => transition.Duration = Math.Max(0.05, v), 0.05, 60, 0.02, unit: " s"));
        Row(section, "Curve", Compact(Ui.Combo(Enum.GetValues<Easing>(), transition.Easing, v => Edit(() => transition.Easing = v))));
    }

    private static string Spaced<T>(T value) where T : struct, Enum => Humanize(value.ToString()).Replace("to black", "to Black");

    private void BuildChannels(LightClip clip)
    {
        var lightTargets = new[] { AutomationTarget.Opacity, AutomationTarget.Intensity, AutomationTarget.HueShift, AutomationTarget.Saturation, AutomationTarget.White };
        var add = new MenuFlyout();
        foreach (var role in new[] { ChannelRole.Pan, ChannelRole.Tilt, ChannelRole.PanTiltSpeed, ChannelRole.Strobe, ChannelRole.Zoom, ChannelRole.Amber, ChannelRole.UV, ChannelRole.ColorWheel, ChannelRole.Gobo, ChannelRole.Effect })
        {
            var item = new MenuItem { Header = Humanize(role.ToString()) };
            item.Click += (_, _) => AddChannelLane(clip, new AutomationLane { Target = AutomationTarget.Role, Role = role });
            add.Items.Add(item);
        }
        add.Items.Add(new Separator());
        foreach (var target in new[] { AutomationTarget.Red, AutomationTarget.Green, AutomationTarget.Blue })
        {
            var item = new MenuItem { Header = $"{target} override" };
            item.Click += (_, _) => AddChannelLane(clip, new AutomationLane { Target = target });
            add.Items.Add(item);
        }
        var byNumber = new MenuItem { Header = "Channel number…" };
        byNumber.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(this) is not Window owner)
                return;
            var text = await Dialogs.Prompt(owner, "Drive a channel", "Channel within each fixture (1-based)", "1");
            if (int.TryParse(text, out var channel) && channel is >= 1 and <= 512)
                AddChannelLane(clip, new AutomationLane { Target = AutomationTarget.Channel, Channel = channel });
        };
        add.Items.Add(byNumber);
        var addButton = new GlyphButton("+", "Drive a fixture channel directly (pan, tilt, strobe…)", () => { });
        addButton.Flyout = add;

        var lanes = clip.Automation.Where(l => !lightTargets.Contains(l.Target)).ToList();
        var section = Section("channels", lanes.Count == 0 ? "Channels" : $"Channels ({lanes.Count})", open: true, addButton);
        if (lanes.Count == 0)
        {
            section.Body.Children.Add(new TextBlock { Text = "+ to drive pan, tilt, strobe or any channel on every fixture that has it.", Foreground = Brushes.Gray, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(20, 2, 0, 4) });
            return;
        }
        foreach (var lane in lanes)
            section.Body.Children.Add(
                LaneProperty(clip, lane.Target, lane.Label, 0, 1, 0.005, 100, "0", "%", lane.Role, lane.Channel, removable: true)
            );
    }

    private void AddChannelLane(LightClip clip, AutomationLane lane)
    {
        if (clip.Automation.Any(l => Same(l, lane.Target, lane.Role, lane.Channel)))
            return;
        Edit(() =>
        {
            lane.Animated = false;
            lane.Keys = [new Keyframe { Value = DefaultFor(lane.Target, lane.Role) }];
            clip.Automation.Add(lane);
        });
        Rebuild();
    }

    private static bool Same(AutomationLane lane, AutomationTarget target, ChannelRole role, int channel) =>
        lane.Target == target && (target != AutomationTarget.Role || lane.Role == role) && (target != AutomationTarget.Channel || lane.Channel == channel);

    private static double DefaultFor(AutomationTarget target, ChannelRole role) =>
        target == AutomationTarget.Role && role is ChannelRole.Pan or ChannelRole.Tilt ? 0.5 : new AutomationLane { Target = target }.DefaultValue;

    /// <summary>
    /// A property backed by an automation lane, with the After Effects stopwatch and keyframe navigator.
    /// No lane means the default value; a static lane holds one value; an animated lane holds keyframes.
    /// </summary>
    private Control LaneProperty(
        LightClip clip,
        AutomationTarget target,
        string name,
        double min,
        double max,
        double step,
        double scale,
        string format,
        string unit,
        ChannelRole role = ChannelRole.Strobe,
        int channel = 1,
        bool removable = false,
        string? tip = null
    )
    {
        AutomationLane? Lane() => clip.Automation.Find(l => Same(l, target, role, channel));
        double Local() => Math.Clamp(_state.Playhead - clip.Start, 0, clip.Duration);
        double Current() => Lane()?.Evaluate(Local()) ?? DefaultFor(target, role);
        Keyframe? KeyHere() => Lane() is { Animated: true } l ? l.Keys.Find(k => Math.Abs(k.Time - Local()) < 1e-3) : null;
        var animated = Lane() is { Animated: true };

        void Set(double value)
        {
            var lane = Lane();
            if (lane is null)
            {
                lane = new AutomationLane { Target = target, Role = role, Channel = channel, Animated = false };
                clip.Automation.Add(lane);
            }
            if (!lane.Animated)
            {
                lane.Keys = [new Keyframe { Time = 0, Value = value }];
                return;
            }
            if (KeyHere() is { } key)
                key.Value = value;
            else
            {
                lane.Keys.Add(new Keyframe { Time = Local(), Value = value });
                lane.SortKeys();
            }
        }

        var scrub = new ScrubValue(Current(), min, max, step, scale, format, unit);
        scrub.Changing += v => Edit(() => Set(v), commit: false);
        scrub.Committed += v => Edit(() => Set(v));

        var stopwatch = new GlyphButton(
            "⏱",
            animated ? "Stop animating — keyframes are removed, the current value stays" : "Animate this property: keyframes go at the playhead",
            () =>
            {
                var value = Current();
                Edit(() =>
                {
                    var lane = Lane();
                    if (lane is { Animated: true })
                    {
                        lane.Animated = false;
                        lane.Keys = [new Keyframe { Value = value }];
                        if (!removable && Math.Abs(value - DefaultFor(target, role)) < 1e-6)
                            clip.Automation.Remove(lane);
                        return;
                    }
                    if (lane is null)
                    {
                        lane = new AutomationLane { Target = target, Role = role, Channel = channel };
                        clip.Automation.Add(lane);
                    }
                    lane.Animated = true;
                    lane.Keys = [new Keyframe { Time = Local(), Value = value }];
                    if (_state.FindClip(clip.Id) is { } found)
                        found.Track.Expanded = true;
                });
                Rebuild();
            },
            active: animated
        );

        var navigator = new StackPanel { Orientation = Orientation.Horizontal };
        if (animated)
        {
            var diamond = new GlyphButton(KeyHere() is null ? "◇" : "◆", "Add or remove a keyframe at the playhead (right-click: curve)", () =>
            {
                Edit(() =>
                {
                    var lane = Lane()!;
                    if (KeyHere() is { } key)
                    {
                        if (lane.Keys.Count > 1)
                            lane.Keys.Remove(key);
                    }
                    else
                        Set(Current());
                });
                Rebuild();
            }, active: true);
            var curve = new ContextMenu();
            foreach (var easing in Enum.GetValues<Easing>())
            {
                var item = new MenuItem { Header = $"{Humanize(easing.ToString())} to next key" };
                item.Click += (_, _) =>
                {
                    if (KeyHere() is { } key)
                        Edit(() => key.Easing = easing);
                };
                curve.Items.Add(item);
            }
            diamond.ContextMenu = curve;
            navigator.Children.Add(new GlyphButton("◀", "Previous keyframe", () => Jump(clip, Lane(), forward: false)));
            navigator.Children.Add(diamond);
            navigator.Children.Add(new GlyphButton("▶", "Next keyframe", () => Jump(clip, Lane(), forward: true)));
            _onPlayhead += () => diamond.Content = KeyHere() is null ? "◇" : "◆";
        }
        if (removable)
            navigator.Children.Add(new GlyphButton("✕", "Stop driving this channel", () =>
            {
                Edit(() => clip.Automation.RemoveAll(l => Same(l, target, role, channel)));
                Rebuild();
            }));
        _onPlayhead += () => scrub.Show(Current());
        return new PropertyRow(name, scrub, stopwatch, navigator, tip);
    }

    private void Jump(LightClip clip, AutomationLane? lane, bool forward)
    {
        if (lane is null || lane.Keys.Count == 0)
            return;
        var local = _state.Playhead - clip.Start;
        var key = forward ? lane.Keys.FirstOrDefault(k => k.Time > local + 1e-3) : lane.Keys.LastOrDefault(k => k.Time < local - 1e-3);
        if (key is not null)
            _state.Seek(clip.Start + key.Time);
    }

    private static string Time(double seconds) => TimelineControl.Timecode(seconds);

    // ---- Media clip ------------------------------------------------------------------------------------

    private void BuildMediaClip(MediaClip clip)
    {
        Title(clip.Name, Path.GetFileName(clip.Path));
        var section = Section("media", "Clip");
        Row(section, "Name", TextField(clip.Name, v => clip.Name = v));
        Row(section, "Start", Scrub(clip.Start, v => clip.Start = Math.Max(0, v), 0, 36000, 0.02, unit: " s"));
        Row(section, "Duration", Scrub(clip.Duration, v => clip.Duration = Math.Max(0.05, v), 0.05, clip.SourceDuration ?? 36000, 0.02, unit: " s"));
        Row(section, "In point", Scrub(clip.MediaOffset, v => clip.MediaOffset = Math.Max(0, v), 0, clip.SourceDuration ?? 36000, 0.02, unit: " s"), "How far into the file the clip starts");
        var audio = Section("media-audio", "Audio");
        Row(audio, "Volume", Scrub(clip.Volume, v => clip.Volume = v, 0, 2, 0.005, 100, "0", "%"));
        if (clip.SourceDuration is { } source)
            audio.Body.Children.Add(new TextBlock { Text = $"Source is {source:0.00} s long", Foreground = Brushes.Gray, FontSize = 11, Margin = new Thickness(20, 4, 0, 0) });
    }

    // ---- Track -----------------------------------------------------------------------------------------

    private void BuildTrack(Track track)
    {
        Title(track.Name, $"{track.Kind} track");
        var section = Section("track", "Track");
        Row(section, "Name", TextField(track.Name, v => track.Name = v));
        Row(section, "Muted", Check(track.Muted, v => track.Muted = v));
        if (track.Kind != TrackKind.Light)
            return;
        Row(section, "Solo", Check(track.Solo, v => track.Solo = v));
        Row(section, "Blend", Compact(Ui.Combo(Enum.GetValues<BlendMode>(), track.Blend, v => Edit(() => track.Blend = v))), "How this track combines with the tracks below it");
        Row(section, "Fixture order", Compact(Ui.Combo(Enum.GetValues<FixtureOrder>(), track.Order, v => Edit(() => track.Order = v), o => Humanize(o.ToString()))), "The order chases, rainbow spread and wipes walk the fixtures");

        var targets = Section("targets", "Targets");
        targets.Body.Children.Add(new TextBlock { Text = "Tip: drag fixtures or groups from the Project panel onto the track.", Foreground = Brushes.Gray, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(20, 0, 0, 4) });
        foreach (var group in _state.Project.Groups)
            targets.Body.Children.Add(TargetCheck($"▣ {group.Name} ({group.FixtureIds.Count})", track.GroupIds, group.Id));
        foreach (var fixture in _state.Project.Fixtures)
            targets.Body.Children.Add(TargetCheck(fixture.Name, track.FixtureIds, fixture.Id));
    }

    private CheckBox TargetCheck(string label, List<string> list, string id)
    {
        var box = new CheckBox { Content = label, IsChecked = list.Contains(id), Margin = new Thickness(20, 0, 0, 0), MinHeight = 24 };
        box.IsCheckedChanged += (_, _) => Edit(() =>
        {
            list.Remove(id);
            if (box.IsChecked == true)
                list.Add(id);
        });
        return box;
    }

    private CheckBox Check(bool value, Action<bool> set)
    {
        var box = new CheckBox { IsChecked = value, MinHeight = 22 };
        box.IsCheckedChanged += (_, _) => Edit(() => set(box.IsChecked == true));
        return box;
    }

    // ---- Fixtures --------------------------------------------------------------------------------------

    private void BuildFixtures(Fixture fixture)
    {
        var project = _state.Project;
        var selected = _state.SelectedFixtureIds.Select(project.FixtureById).OfType<Fixture>().ToList();
        var profile = project.ProfileOf(fixture);
        var single = selected.Count == 1;
        Title(single ? fixture.Name : $"{selected.Count} fixtures", single ? $"{profile?.DisplayName} · DMX {fixture.Universe}.{fixture.Address:000}" : "Edits apply to all of them");

        if (single)
        {
            var patch = Section("patch", "Patch");
            Row(patch, "Name", TextField(fixture.Name, v => fixture.Name = v));
            Row(patch, "Profile", Compact(Ui.Combo<FixtureProfile>(project.Profiles, profile!, p => Edit(() => fixture.ProfileId = p.Id), p => p.DisplayName)));
            Row(patch, "Universe", Scrub(fixture.Universe, v => fixture.Universe = (int)Math.Round(v), 1, 64, 0.1, format: "0"));
            var address = Scrub(fixture.Address, v => fixture.Address = (int)Math.Round(v), 1, 512, 0.25, format: "0");
            var next = new GlyphButton("Next free", "Move to the first free address in this universe", () =>
            {
                var free = project.NextFreeAddress(fixture.Universe, profile?.Channels.Count ?? 1, fixture.Id);
                if (free > 0)
                    Edit(() => fixture.Address = free);
                Rebuild();
            });
            Row(patch, "Address", new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { address, next } });
            if (profile?.Has(ChannelRole.White) == true)
                Row(patch, "Auto white", Check(fixture.ExtractWhite, v => fixture.ExtractWhite = v), "Moves the shared part of R/G/B onto the white LED");
            if (profile is { HasColor: false })
                Row(patch, "Gel", Ui.ColorPicker(fixture.GelColor, hex => Edit(() => fixture.GelColor = hex, commit: false), () => _state.Commit()));
        }

        var transform = Section("transform", "Transform");
        if (single)
        {
            Row(transform, "Position X", Scrub(fixture.Position.X, v => fixture.Position = fixture.Position with { X = Math.Round(v, 3) }, -200, 200, 0.01, unit: " m"));
            Row(transform, "Position Y", Scrub(fixture.Position.Y, v => fixture.Position = fixture.Position with { Y = Math.Round(v, 3) }, -10, 100, 0.01, unit: " m"), "Height above the floor");
            Row(transform, "Position Z", Scrub(fixture.Position.Z, v => fixture.Position = fixture.Position with { Z = Math.Round(v, 3) }, -200, 200, 0.01, unit: " m"), "Toward the audience is +");
        }
        else
            Row(transform, "Height (all)", Scrub(fixture.Position.Y, v => selected.ForEach(f => f.Position = f.Position with { Y = Math.Round(v, 3) }), -10, 100, 0.01, unit: " m"));
        Row(transform, "Yaw", Scrub(fixture.Yaw, v => selected.ForEach(f => f.Yaw = Math.Round(v, 1)), -360, 360, 0.5, format: "0.#", unit: "°"), "0° faces the audience");
        Row(transform, "Pitch", Scrub(fixture.Pitch, v => selected.ForEach(f => f.Pitch = Math.Round(v, 1)), -180, 90, 0.5, format: "0.#", unit: "°"), "-90° points straight down");

        var tools = Section("rig-tools", "Rig tools", open: false);
        double ax = 0, ay = 1, az = 0;
        var aimPoint = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                AimScrub(ax, v => ax = v),
                AimScrub(ay, v => ay = v),
                AimScrub(az, v => az = v),
                new GlyphButton("Aim", "Point the selected fixtures at this X / Y / Z", () =>
                {
                    Edit(() => selected.ForEach(f => StageMath.AimAt(f, new Vec3(ax, ay, az))));
                    Rebuild();
                }),
            },
        };
        Row(tools, "Aim at", aimPoint, "Or use the Aim tool (A) in the stage view and click a spot");
        var trusses = project.Stage.Objects.Where(o => o.Kind == StageObjectKind.Truss).ToList();
        if (trusses.Count > 0)
        {
            var truss = trusses[0];
            var combo = Compact(Ui.Combo(trusses, truss, t => truss = t, t => t.Name));
            var hang = new GlyphButton("Hang", "Spread the selected fixtures evenly under the truss", () =>
            {
                Edit(() => StageMath.DistributeAlong(truss, selected.OrderBy(f => f.Position.X).ToList()));
                Rebuild();
            });
            Row(tools, "Distribute on", new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { combo, hang } });
        }

        if (single && profile is not null)
        {
            var dmx = Section("dmx", "DMX output", open: false);
            var values = new TextBlock { FontFamily = new FontFamily("Consolas, Menlo, monospace"), FontSize = 11, Foreground = Brushes.Gainsboro, Margin = new Thickness(20, 0, 0, 0) };
            void ShowValues()
            {
                var slice = _state.CurrentFrame.Slice(fixture, profile.Channels.Count).ToArray();
                values.Text = string.Join("\n", profile.Channels.Select((c, i) => $"{fixture.Address + i,3}  {c.Name,-12} {(i < slice.Length ? slice[i] : 0),3}"));
            }
            ShowValues();
            _onPlayhead += ShowValues;
            dmx.Body.Children.Add(values);
        }
    }

    private static ScrubValue AimScrub(double value, Action<double> set)
    {
        var scrub = new ScrubValue(value, -100, 100, 0.02, unit: " m");
        scrub.Changing += set;
        scrub.Committed += set;
        return scrub;
    }

    // ---- Stage object ----------------------------------------------------------------------------------

    private void BuildObject(StageObject obj)
    {
        Title(obj.Name, $"{obj.Kind} · top at {obj.Position.Y + obj.Size.Y / 2:0.00} m");
        var section = Section("object", "Object");
        Row(section, "Name", TextField(obj.Name, v => obj.Name = v));
        Row(section, "Kind", Compact(Ui.Combo(Enum.GetValues<StageObjectKind>(), obj.Kind, v => Edit(() => obj.Kind = v))));
        Row(section, "Colour", Ui.ColorPicker(obj.Color, hex => Edit(() => obj.Color = hex, commit: false), () => _state.Commit()));

        var transform = Section("object-transform", "Transform");
        Row(transform, "Position X", Scrub(obj.Position.X, v => obj.Position = obj.Position with { X = Math.Round(v, 3) }, -200, 200, 0.01, unit: " m"));
        Row(transform, "Position Y", Scrub(obj.Position.Y, v => obj.Position = obj.Position with { Y = Math.Round(v, 3) }, -10, 100, 0.01, unit: " m"), "Height of the centre");
        Row(transform, "Position Z", Scrub(obj.Position.Z, v => obj.Position = obj.Position with { Z = Math.Round(v, 3) }, -200, 200, 0.01, unit: " m"));
        Row(transform, obj.Kind == StageObjectKind.Truss ? "Length" : "Width", Scrub(obj.Size.X, v => obj.Size = obj.Size with { X = Math.Max(0.05, Math.Round(v, 3)) }, 0.05, 200, 0.01, unit: " m"));
        Row(transform, "Height", Scrub(obj.Size.Y, v => obj.Size = obj.Size with { Y = Math.Max(0.02, Math.Round(v, 3)) }, 0.02, 100, 0.01, unit: " m"));
        Row(transform, "Depth", Scrub(obj.Size.Z, v => obj.Size = obj.Size with { Z = Math.Max(0.02, Math.Round(v, 3)) }, 0.02, 200, 0.01, unit: " m"));
        Row(transform, "Yaw", Scrub(obj.Yaw, v => obj.Yaw = Math.Round(v, 1), -360, 360, 0.5, format: "0.#", unit: "°"));

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(20, 4, 0, 0) };
        if (obj.Kind == StageObjectKind.Platform)
            actions.Children.Add(Ui.Button("Sit on floor", () =>
            {
                Edit(() => obj.Position = obj.Position with { Y = obj.Size.Y / 2 });
                Rebuild();
            }));
        actions.Children.Add(Ui.Button("Duplicate", () =>
        {
            var copy = obj.Clone();
            copy.Name += " copy";
            copy.Position += new Vec3(0.5, 0, 0);
            _state.Project.Stage.Objects.Add(copy);
            _state.Commit();
            _state.SelectObject(copy.Id);
        }));
        transform.Body.Children.Add(actions);
    }

    // ---- Nothing selected ------------------------------------------------------------------------------

    private void BuildEmpty()
    {
        _root.Children.Add(
            new StackPanel
            {
                Margin = new Thickness(0, 60, 0, 0),
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    new TextBlock { Text = "(no selection)", Foreground = Brushes.Gray, HorizontalAlignment = HorizontalAlignment.Center },
                    new TextBlock
                    {
                        Text = "Select a clip, track, fixture or set piece\nto see its controls here.",
                        Foreground = Brushes.DimGray,
                        FontSize = 11,
                        TextAlignment = TextAlignment.Center,
                    },
                    Ui.Button("Project settings…", () => OpenProjectSettings?.Invoke()),
                },
            }
        );
    }
}
