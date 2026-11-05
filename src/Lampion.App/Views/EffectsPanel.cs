using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Lampion.App.Controls;
using Lampion.App.Editor;
using Lampion.Core.Model;
using Lampion.Core.Persistence;
using LightEffect = Lampion.Core.Model.Effect;

namespace Lampion.App.Views;

/// <summary>
/// Premiere's Effects panel: light effects and transitions in folders, searchable. Drag an effect onto a
/// light track to make a clip, or a transition onto a clip to blend into it. Double-click works too.
/// </summary>
public sealed class EffectsPanel : DockPanel
{
    private readonly EditorState _state;
    private readonly TimelineControl _timeline;
    private readonly TextBox _search = new() { PlaceholderText = "🔍  Search effects", MinHeight = 24, Margin = new Thickness(6, 6, 6, 4) };
    private readonly StackPanel _list = new() { Spacing = 1, Margin = new Thickness(4, 0, 4, 8) };

    public EffectsPanel(EditorState state, TimelineControl timeline)
    {
        _state = state;
        _timeline = timeline;
        SetDock(_search, Dock.Top);
        Children.Add(_search);
        Children.Add(new ScrollViewer { Content = _list });
        _search.TextChanged += (_, _) => Fill();
        Fill();
    }

    private void Fill()
    {
        _list.Children.Clear();
        var filter = (_search.Text ?? "").Trim();
        bool Match(string text) => filter.Length == 0 || text.Contains(filter, StringComparison.OrdinalIgnoreCase);

        var effects = LightEffect.Catalog.Where(e => Match(e.Name) || Match(e.Description)).ToList();
        if (effects.Count > 0)
        {
            var folder = TwirlSection.Titled("fx-light", "📁  Light effects");
            foreach (var (kind, name, description) in effects)
                folder.Body.Children.Add(Item(EffectIcon(LightEffect.Create(kind)), name, description, () => AddEffect(kind), p => _timeline.PreviewDrop(p, DropKind.Effect), p => _timeline.Drop(kind, p)));
            _list.Children.Add(folder);
        }

        var presets = PresetActions.Library().Where(p => Match(p.Preset.Name) || Match(p.Preset.Description) || Match(p.Preset.Effect.DisplayName)).ToList();
        var import = new GlyphButton("Import…", "Add a preset file (" + EffectPreset.Extension + ") to your presets", () => WithOwner(PresetActions.Import));
        var presetFolder = TwirlSection.Titled("fx-presets", "📁  Presets", true, import);
        foreach (var (path, preset) in presets)
        {
            var tip = (preset.Description.Length > 0 ? preset.Description + "\n" : "") + $"{preset.Effect.DisplayName}, {preset.Duration:0.##} s" + (path is null ? " · built in" : "");
            var row = Item(EffectIcon(preset.Effect), preset.Name, tip, () => AddPreset(preset), p => _timeline.PreviewDrop(p, DropKind.Effect, preset.Duration), p => _timeline.DropPreset(preset, p));
            row.ContextMenu = PresetMenu(path, preset);
            presetFolder.Body.Children.Add(row);
        }
        if (filter.Length == 0 && presets.All(p => p.Path is null))
            presetFolder.Body.Children.Add(new TextBlock { Text = "Right-click a clip → Save as preset… to add your own.", Foreground = Brushes.Gray, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6, 4) });
        if (presets.Count > 0 || filter.Length == 0)
            _list.Children.Add(presetFolder);

        var transitions = new (TransitionKind Kind, string Name, string Description)[]
        {
            (TransitionKind.Crossfade, "Crossfade", "Blend straight from one clip into the next"),
            (TransitionKind.DipToBlack, "Dip to black", "Fade out, then the next clip fades in"),
            (TransitionKind.Wipe, "Wipe", "The next clip sweeps across the fixtures in track order"),
        }.Where(t => Match(t.Name) || Match(t.Description)).ToList();
        if (transitions.Count > 0)
        {
            var folder = TwirlSection.Titled("fx-transitions", "📁  Transitions");
            foreach (var (kind, name, description) in transitions)
                folder.Body.Children.Add(Item(new TextBlock { Text = "⧖", FontSize = 13 }, name, description, () => ApplyTransition(kind), p => _timeline.PreviewDrop(p, DropKind.Transition), p => _timeline.DropTransition(kind, p)));
            _list.Children.Add(folder);
        }

        if (_list.Children.Count == 0)
            _list.Children.Add(new TextBlock { Text = "Nothing matches.", Foreground = Brushes.Gray, Margin = new Thickness(8) });
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UserData.PresetsChanged += Fill;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        UserData.PresetsChanged -= Fill;
        base.OnDetachedFromVisualTree(e);
    }

    private static Control EffectIcon(LightEffect effect)
    {
        var chip = new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(3), Background = Ui.Brush(effect.SwatchColor) };
        IReadOnlyList<string>? stops = effect switch
        {
            RainbowEffect => ["#FF0000", "#00FF00", "#0000FF"],
            GradientEffect g => g.Colors,
            ColorCycleEffect c => c.Colors,
            ColorWaveEffect w => [w.Base, w.Color],
            FireEffect f => [f.Ember, f.Flame],
            _ => null,
        };
        if (stops is { Count: > 1 })
        {
            var gradient = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            };
            for (var i = 0; i < stops.Count; i++)
                gradient.GradientStops.Add(new GradientStop(Color.TryParse(stops[i], out var c) ? c : Colors.White, i / (stops.Count - 1.0)));
            chip.Background = gradient;
        }
        return chip;
    }

    private ContextMenu PresetMenu(string? path, EffectPreset preset)
    {
        var menu = new ContextMenu();
        MenuItem Add(string header, Action action)
        {
            var item = new MenuItem { Header = header };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
            return item;
        }
        Add("Add at playhead", () => AddPreset(preset));
        var apply = Add("Apply to selected clip", () =>
        {
            if (_state.PrimaryClip is LightClip clip)
                TimelineOps.ApplyPreset(_state, clip, preset);
        });
        menu.Opening += (_, _) => apply.IsEnabled = _state.PrimaryClip is LightClip;
        menu.Items.Add(new Separator());
        Add("Export…", () => WithOwner(owner => PresetActions.Export(owner, preset)));
        if (path is not null)
        {
            Add("Rename…", () => WithOwner(owner => PresetActions.Rename(owner, path, preset)));
            Add("Delete…", () => WithOwner(owner => PresetActions.Delete(owner, path, preset)));
        }
        return menu;
    }

    private void WithOwner(Func<Window, Task> action)
    {
        if (TopLevel.GetTopLevel(this) is Window owner)
            _ = action(owner);
    }

    private Control Item(Control icon, string name, string description, Action onDoubleClick, Action<Point?> preview, Func<Point, bool> drop)
    {
        icon.VerticalAlignment = VerticalAlignment.Center;
        var row = new Border
        {
            Padding = new Thickness(6, 4),
            CornerRadius = new CornerRadius(3),
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { icon, new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center } } },
        };
        ToolTip.SetTip(row, description + "\nDrag onto the timeline, or double-click.");
        row.PointerEntered += (_, _) => row.Background = new SolidColorBrush(Color.FromRgb(0x33, 0x35, 0x3A));
        row.PointerExited += (_, _) => row.Background = Brushes.Transparent;

        var dragging = false;
        row.PointerPressed += (_, e) =>
        {
            // Right-click opens the context menu; only the left button picks the item up.
            if (!e.GetCurrentPoint(row).Properties.IsLeftButtonPressed)
                return;
            if (e.ClickCount == 2)
            {
                onDoubleClick();
                return;
            }
            dragging = true;
            e.Pointer.Capture(row);
        };
        row.PointerMoved += (_, e) =>
        {
            if (dragging)
                preview(e.GetPosition(_timeline));
        };
        row.PointerReleased += (_, e) =>
        {
            if (!dragging)
                return;
            dragging = false;
            e.Pointer.Capture(null);
            preview(null);
            drop(e.GetPosition(_timeline));
        };
        return row;
    }

    private void AddEffect(string kind)
    {
        TimelineOps.AddEffectClip(_state, TargetTrack(), kind, _state.Playhead);
    }

    private void AddPreset(EffectPreset preset) => TimelineOps.AddPresetClip(_state, TargetTrack(), preset, _state.Playhead);

    private Track TargetTrack() =>
        _state.SelectedTrack is { Kind: TrackKind.Light } selected ? selected : _state.Project.Tracks.FirstOrDefault(t => t.Kind == TrackKind.Light) ?? TimelineOps.AddLightTrack(_state);

    private void ApplyTransition(TransitionKind kind)
    {
        if (_state.PrimaryClip is LightClip clip)
            TimelineOps.SetTransition(_state, clip, kind);
    }
}
