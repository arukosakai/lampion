using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Lampion.App.Controls;
using Lampion.App.Editor;
using Lampion.App.Rendering;
using Lampion.Core.Model;

namespace Lampion.App.Views;

/// <summary>The 3D viewport with the stage builder toolbar above it.</summary>
public sealed class StagePanel : DockPanel
{
    private readonly EditorState _state;

    public StagePanel(EditorState state)
    {
        _state = state;
        View = new StageView(state);
        var overlay = new StageOverlay(View, state);

        var presets = new List<ToggleButton>();
        ToggleButton Preset(string label, ViewPreset preset, string key)
        {
            var toggle = new ToggleButton { Content = label, IsChecked = preset == ViewPreset.Perspective };
            ToolTip.SetTip(toggle, $"{label} view ({key})");
            toggle.Click += (_, _) => SetPreset(preset);
            presets.Add(toggle);
            return toggle;
        }

        var move = new ToggleButton { Content = "Move", IsChecked = true };
        var aim = new ToggleButton { Content = "Aim" };
        ToolTip.SetTip(move, "Drag fixtures and set pieces (W)");
        ToolTip.SetTip(aim, "Click on the stage to point the selected fixtures there (A)");
        move.Click += (_, _) => SetTool(BuilderTool.Move);
        aim.Click += (_, _) => SetTool(BuilderTool.Aim);
        _moveToggle = move;
        _aimToggle = aim;

        var snap = Ui.Combo([0.05, 0.1, 0.25, 0.5, 1.0], state.Project.Stage.GridStep, v =>
        {
            state.Project.Stage.GridStep = v;
            state.Commit();
        }, v => $"{v:0.##} m");
        snap.Width = 86;
        ToolTip.SetTip(snap, "Snap step for moving things");

        var addMenu = new MenuFlyout();
        foreach (var kind in Enum.GetValues<StageObjectKind>())
        {
            var item = new MenuItem { Header = $"{kind}" };
            item.Click += (_, _) => AddObject(kind);
            addMenu.Items.Add(item);
        }
        var add = new Button { Content = "+ Set piece", Flyout = addMenu };

        var build = Ui.Toggle("Build", View.BuildMode, on => View.BuildMode = on, "Edit the stage layout; turn off to just watch the show");
        var labels = Ui.Toggle("Labels", View.ShowLabels, on => View.ShowLabels = on);
        var bounce = Ui.Toggle("Bounce", state.Project.Stage.LightBounce, on =>
        {
            if (state.Project.Stage.LightBounce == on)
                return;
            state.Project.Stage.LightBounce = on;
            state.Commit();
        }, "Let the lit rig spill a little coloured light into the whole room");
        state.ProjectReplaced += () => bounce.IsChecked = state.Project.Stage.LightBounce;
        var haze = Ui.Slider(state.Project.Stage.Haze, 0, 1, v => state.Project.Stage.Haze = v, () => state.Commit());
        haze.Width = 90;
        ToolTip.SetTip(haze, "Haze — how visible beams are in the air");
        state.ProjectReplaced += () => haze.Value = state.Project.Stage.Haze;

        var toolbar = new WrapPanel { Margin = new Thickness(6, 4) };
        foreach (var c in new Control[]
                 {
                     Preset("Persp", ViewPreset.Perspective, "1"), Preset("Top", ViewPreset.Top, "2"), Preset("Front", ViewPreset.Front, "3"), Preset("Side", ViewPreset.Side, "4"),
                     Spacer(), build, move, aim, Spacer(), Ui.Label("Snap"), snap, add, Spacer(), labels, bounce, Ui.Label("Haze"), haze,
                 })
        {
            c.Margin = new Thickness(0, 0, 3, 0);
            c.VerticalAlignment = VerticalAlignment.Center;
            toolbar.Children.Add(c);
        }
        _presetToggles = presets;

        SetDock(toolbar, Dock.Top);
        Children.Add(toolbar);
        var stack = new Panel();
        stack.Children.Add(View);
        stack.Children.Add(overlay);
        Children.Add(stack);
    }

    private readonly List<ToggleButton> _presetToggles;
    private readonly ToggleButton _moveToggle;
    private readonly ToggleButton _aimToggle;

    public StageView View { get; }

    private static Control Spacer() => new Border { Width = 8 };

    public void SetPreset(ViewPreset preset)
    {
        View.ApplyPreset(preset);
        for (var i = 0; i < _presetToggles.Count; i++)
            _presetToggles[i].IsChecked = (int)preset == i;
    }

    public void SetTool(BuilderTool tool)
    {
        View.Tool = tool;
        _moveToggle.IsChecked = tool == BuilderTool.Move;
        _aimToggle.IsChecked = tool == BuilderTool.Aim;
    }

    private void AddObject(StageObjectKind kind)
    {
        var target = View.Camera.Target;
        var obj = kind switch
        {
            StageObjectKind.Truss => new StageObject { Name = "Truss", Kind = kind, Size = new Vec3(4, 0.3, 0.3), Position = new Vec3(0, 6, 0), Color = "#B8BCC4" },
            StageObjectKind.Wall => new StageObject { Name = "Wall", Kind = kind, Size = new Vec3(4, 3, 0.15), Position = new Vec3(0, 1.5, -4), Color = "#1E1E24" },
            StageObjectKind.Screen => new StageObject { Name = "Screen", Kind = kind, Size = new Vec3(3.2, 1.8, 0.08), Position = new Vec3(0, 2.5, -4.5), Color = "#050505" },
            _ => new StageObject { Name = "Riser", Kind = kind, Size = new Vec3(2, 0.4, 2), Position = new Vec3(0, 1.2, -2), Color = "#2B2622" },
        };
        obj.Position = obj.Position with { X = StageMath.Snap(target.X, 0.5) };
        _state.Project.Stage.Objects.Add(obj);
        _state.Commit();
        _state.SelectObject(obj.Id);
    }
}
