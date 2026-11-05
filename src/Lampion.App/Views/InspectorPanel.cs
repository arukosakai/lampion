using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Lampion.App.Editor;
using Lampion.App.Views.Inspector;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Views;

/// <summary>
/// Effect Controls, modelled on After Effects: twirl-down sections, blue values you scrub or type, and a
/// stopwatch on every animatable property. With the stopwatch on, changing a value at the playhead sets a
/// keyframe there; ◀ ◆ ▶ step between keys.
/// </summary>
/// <remarks>
/// A thin host: it picks the editor for the selection and rebuilds the control tree only when the selection
/// or the project changes. Playhead changes just refresh the live parts, without allocating.
/// </remarks>
internal sealed class InspectorPanel : ScrollViewer
{
    private readonly StackPanel _root = new() { Spacing = 0, Margin = new Thickness(6, 4, 8, 16) };
    private readonly EditorState _state;
    private readonly InspectorContext _context;
    private readonly LightClipEditor _lightClip;
    private readonly MediaClipEditor _mediaClip;
    private readonly TrackEditor _track;
    private readonly FixtureEditor _fixtures;
    private readonly StageObjectEditor _stageObject;

    /// <summary>Creates the panel and keeps it in step with <paramref name="state"/> for the panel's lifetime.</summary>
    public InspectorPanel(EditorState state)
    {
        _state = state;
        _context = new InspectorContext(state, this, Rebuild);
        var fields = new InspectorFields(_context);
        _lightClip = new LightClipEditor(fields);
        _mediaClip = new MediaClipEditor(fields);
        _track = new TrackEditor(fields);
        _fixtures = new FixtureEditor(fields);
        _stageObject = new StageObjectEditor(fields);

        Content = _root;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        state.SelectionChanged += Rebuild;
        state.ProjectReplaced += Rebuild;
        state.Changed += OnModelChanged;
        state.PlayheadChanged += _context.RefreshAll;
        Rebuild();
    }

    /// <summary>Opens the project settings; the main window supplies it so this panel needs no window of its own.</summary>
    public Action? OpenProjectSettings { get; set; }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(ScrollViewer);

    // Edits made here, or while the user is typing in here, must not rebuild the controls under them.
    private void OnModelChanged()
    {
        if (!_context.IsEditing && !IsKeyboardFocusWithin)
            Rebuild();
    }

    private void Rebuild()
    {
        _root.Children.Clear();
        _context.ClearTracked();
        switch (_state.SelectionKind)
        {
            case SelectionKind.Clip when _state.PrimaryClip is LightClip light:
                _lightClip.Build(_root, light);
                break;
            case SelectionKind.Clip when _state.PrimaryClip is MediaClip media:
                _mediaClip.Build(_root, media);
                break;
            case SelectionKind.Track when _state.SelectedTrack is { } track:
                _track.Build(_root, track);
                break;
            case SelectionKind.Fixture when _state.PrimaryFixture is { } fixture:
                _fixtures.Build(_root, fixture);
                break;
            case SelectionKind.StageObject when _state.SelectedObject is { } obj:
                _stageObject.Build(_root, obj);
                break;
            default:
                _root.Children.Add(EmptySelectionView.Create(() => OpenProjectSettings?.Invoke()));
                break;
        }
    }
}
