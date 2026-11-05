using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lampion.App.Controls;
using Lampion.App.Editor;
using Lampion.App.Views.EffectsLibrary;
using Lampion.Core.Model.Effects;
using Lampion.Core.Model.Timeline;
using Lampion.Core.Persistence;

namespace Lampion.App.Views;

/// <summary>
/// Effects &amp; Presets: light effects, presets and transitions in folders, searchable. Drag an effect onto a
/// light track to make a clip, or a transition onto a clip to blend into it. Double-click works too.
/// </summary>
internal sealed class EffectsPanel : DockPanel
{
    private readonly EditorState _state;
    private readonly TimelineControl _timeline;
    private readonly TextBox _search = new() { PlaceholderText = "🔍  Search effects", MinHeight = 24, Margin = new Thickness(6, 6, 6, 4) };
    private readonly StackPanel _list = new() { Spacing = 1, Margin = new Thickness(4, 0, 4, 8) };

    /// <summary>Creates the panel; it refills itself when the search text or the user's presets change.</summary>
    /// <param name="state">The editor state clips are added through.</param>
    /// <param name="timeline">The timeline entries are dragged onto.</param>
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

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UserData.PresetsChanged += Fill;
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        UserData.PresetsChanged -= Fill;
        base.OnDetachedFromVisualTree(e);
    }

    private void Fill()
    {
        _list.Children.Clear();
        var filter = (_search.Text ?? "").Trim();
        bool Match(string text) => filter.Length == 0 || text.Contains(filter, StringComparison.OrdinalIgnoreCase);

        AddEffects(Match);
        AddPresets(Match, filter.Length == 0);
        AddTransitions(Match);

        if (_list.Children.Count == 0)
            _list.Children.Add(new TextBlock { Text = "Nothing matches.", Foreground = Brushes.Gray, Margin = new Thickness(8) });
    }

    private void AddEffects(Func<string, bool> match)
    {
        var effects = EffectCatalog.Entries.Where(e => match(e.Name) || match(e.Description)).ToList();
        if (effects.Count == 0)
            return;
        var folder = TwirlSection.Titled("fx-light", "📁  Light effects");
        foreach (var (kind, name, description) in effects)
            folder.Body.Children.Add(Item(EffectSwatch.Create(EffectCatalog.Create(kind)), name, description, () => AddEffect(kind), p => _timeline.PreviewDrop(p, DropKind.Effect), p => _timeline.Drop(kind, p)));
        _list.Children.Add(folder);
    }

    private void AddPresets(Func<string, bool> match, bool unfiltered)
    {
        var presets = PresetActions.Library().Where(p => match(p.Preset.Name) || match(p.Preset.Description) || match(p.Preset.Effect.DisplayName)).ToList();
        var import = new GlyphButton("Import…", "Add a preset file (" + EffectPreset.Extension + ") to your presets", () => WithOwner(PresetActions.Import));
        var folder = TwirlSection.Titled("fx-presets", "📁  Presets", true, import);
        foreach (var (path, preset) in presets)
        {
            var tip = (preset.Description.Length > 0 ? preset.Description + "\n" : "") + $"{preset.Effect.DisplayName}, {preset.Duration:0.##} s" + (path is null ? " · built in" : "");
            var row = Item(EffectSwatch.Create(preset.Effect), preset.Name, tip, () => AddPreset(preset), p => _timeline.PreviewDrop(p, DropKind.Effect, preset.Duration), p => _timeline.DropPreset(preset, p));
            row.ContextMenu = PresetMenu.Create(_state, path, preset, () => AddPreset(preset), WithOwner);
            folder.Body.Children.Add(row);
        }
        if (unfiltered && presets.All(p => p.Path is null))
            folder.Body.Children.Add(new TextBlock { Text = "Right-click a clip → Save as preset… to add your own.", Foreground = Brushes.Gray, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6, 4) });
        if (presets.Count > 0 || unfiltered)
            _list.Children.Add(folder);
    }

    private void AddTransitions(Func<string, bool> match)
    {
        var transitions = TransitionEntry.All.Where(t => match(t.Name) || match(t.Description)).ToList();
        if (transitions.Count == 0)
            return;
        var folder = TwirlSection.Titled("fx-transitions", "📁  Transitions");
        foreach (var (kind, name, description) in transitions)
            folder.Body.Children.Add(Item(new TextBlock { Text = "⧖", FontSize = 13 }, name, description, () => ApplyTransition(kind), p => _timeline.PreviewDrop(p, DropKind.Transition), p => _timeline.DropTransition(kind, p)));
        _list.Children.Add(folder);
    }

    private LibraryItem Item(Control icon, string name, string description, Action onDoubleClick, Action<Point?> preview, Func<Point, bool> drop) =>
        new(icon, name, description, _timeline, onDoubleClick, preview, drop);

    private void WithOwner(Func<Window, Task> action)
    {
        if (TopLevel.GetTopLevel(this) is Window owner)
            _ = action(owner);
    }

    private void AddEffect(string kind) => TimelineOps.AddEffectClip(_state, TargetTrack(), kind, _state.Playhead);

    private void AddPreset(EffectPreset preset) => TimelineOps.AddPresetClip(_state, TargetTrack(), preset, _state.Playhead);

    private Track TargetTrack() =>
        _state.SelectedTrack is { Kind: TrackKind.Light } selected ? selected : _state.Project.Tracks.FirstOrDefault(t => t.Kind == TrackKind.Light) ?? TimelineOps.AddLightTrack(_state);

    private void ApplyTransition(TransitionKind kind)
    {
        if (_state.PrimaryClip is LightClip clip)
            TimelineOps.SetTransition(_state, clip, kind);
    }
}
