using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Lampion.App.Editor;

namespace Lampion.App.Views.ProfileEditor;

/// <summary>
/// Where the test values go besides the preview: nowhere, every patched fixture of this profile, or one of
/// them. While a target is picked, <see cref="EditorState.Test"/> holds the values on those fixtures.
/// </summary>
internal sealed class TestOutputPicker : StackPanel
{
    private readonly EditorState _state;
    private readonly ProfileEditSession _session;
    private IReadOnlySet<string> _targets = new HashSet<string>();

    /// <param name="state">Receives the held values in <see cref="EditorState.Test"/>.</param>
    /// <param name="session">Supplies the patched fixtures and the values.</param>
    public TestOutputPicker(EditorState state, ProfileEditSession session)
    {
        _state = state;
        _session = session;
        Orientation = Orientation.Horizontal;

        var patched = session.Patched;
        var outputs = new List<(string Label, HashSet<string> Ids)> { ("Preview only", []) };
        if (patched.Count > 1)
            outputs.Add(($"All {patched.Count} patched fixtures", patched.Select(f => f.Id).ToHashSet()));
        outputs.AddRange(patched.Select(f => ($"{f.Name}  ·  {f.Universe}.{f.Address:000}", new HashSet<string> { f.Id })));
        var output = Ui.Combo(outputs, outputs[0], o =>
        {
            _targets = o.Ids;
            Push();
        }, o => o.Label);
        output.IsEnabled = patched.Count > 0;
        output.MinWidth = 200;
        output.HorizontalAlignment = HorizontalAlignment.Right;
        ToolTip.SetTip(output, patched.Count == 0
            ? "Patch fixtures with this profile to test them live"
            : "Hold these values on the patched fixtures while the editor is open: the stage view shows them, and Art-Net sends them when it's on");

        Children.Add(new TextBlock { Text = "Output", Foreground = ProfileEditorUi.Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        Children.Add(output);

        session.ValuesChanged += Push;
    }

    // EditorState re-renders its tested frame only for a new FixtureTest, so live output copies the values
    // on each change; with "Preview only" nothing is allocated.
    private void Push() => _state.Test = _targets.Count == 0 ? null : new FixtureTest(_targets, _session.Test.ToArray());
}
