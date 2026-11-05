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
                folder.Body.Children.Add(Item(EffectIcon(kind), name, description, () => AddEffect(kind), p => _timeline.PreviewDrop(p, DropKind.Effect), p => _timeline.Drop(kind, p)));
            _list.Children.Add(folder);
        }

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

    private static Control EffectIcon(string kind)
    {
        var chip = new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(3), Background = Ui.Brush(LightEffect.Create(kind).SwatchColor) };
        if (kind == "rainbow")
            chip.Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Colors.Red, 0), new GradientStop(Colors.Lime, 0.5), new GradientStop(Colors.Blue, 1) },
            };
        return chip;
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
        var track = _state.SelectedTrack is { Kind: TrackKind.Light } selected ? selected : _state.Project.Tracks.FirstOrDefault(t => t.Kind == TrackKind.Light) ?? TimelineOps.AddLightTrack(_state);
        TimelineOps.AddEffectClip(_state, track, kind, _state.Playhead);
    }

    private void ApplyTransition(TransitionKind kind)
    {
        if (_state.PrimaryClip is LightClip clip)
            TimelineOps.SetTransition(_state, clip, kind);
    }
}
