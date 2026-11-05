using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Lampion.App.Controls;
using Lampion.App.Editor;
using Lampion.App.Rendering;
using Lampion.Core.Model.Stage;

namespace Lampion.App.Views;

/// <summary>
/// The 3D viewport with a slim Unity-style tool strip above it and the scene gizmo in its top-right corner.
/// </summary>
internal sealed class StagePanel : DockPanel
{
    private static readonly double[] SnapSteps = [0.05, 0.1, 0.25, 0.5, 1.0];

    private readonly EditorState _state;
    private readonly Dictionary<BuilderTool, ToggleButton> _tools = [];
    private readonly Button _snapButton;

    /// <summary>A stage panel over <paramref name="state"/>.</summary>
    public StagePanel(EditorState state)
    {
        _state = state;
        View = new StageView(state);
        var overlay = new StageOverlay(View, state);
        var gizmo = new ViewGizmo(View) { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 8, 10, 0) };

        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 1 };
        tools.Children.Add(Tool(BuilderTool.Hand, "✋", "Hand (Q) — look around without editing"));
        tools.Children.Add(Tool(BuilderTool.Move, "✥", "Move (W) — drag fixtures and set pieces; Alt+drag for height"));
        tools.Children.Add(Tool(BuilderTool.Aim, "◎", "Aim (A) — click the stage to point the selected fixtures there"));

        _snapButton = new Button { Content = SnapLabel(), Padding = new Thickness(8, 1), Flyout = SnapMenu() };
        ToolTip.SetTip(_snapButton, "Snap step for moving things");

        var add = new Button { Content = "＋", Padding = new Thickness(8, 1), Flyout = AddMenu() };
        ToolTip.SetTip(add, "Add a set piece: truss, riser, wall or screen");

        var viewMenu = new Button { Content = "View ▾", Padding = new Thickness(8, 1), Flyout = new Flyout { Content = new StageViewOptions(View, state), Placement = PlacementMode.BottomEdgeAlignedRight } };
        ToolTip.SetTip(viewMenu, "Labels, light bounce, haze and stage quality");

        var bar = new DockPanel { Height = 30, Background = new SolidColorBrush(Color.FromRgb(0x24, 0x24, 0x26)), LastChildFill = false };
        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(6, 0), VerticalAlignment = VerticalAlignment.Center, Children = { tools, Divider(), _snapButton, add } };
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center, Children = { GizmoToggle(gizmo), viewMenu } };
        SetDock(left, Dock.Left);
        SetDock(right, Dock.Right);
        bar.Children.Add(left);
        bar.Children.Add(right);

        SetDock(bar, Dock.Top);
        Children.Add(bar);
        var stack = new Panel();
        stack.Children.Add(View);
        stack.Children.Add(overlay);
        stack.Children.Add(gizmo);
        Children.Add(stack);
        SetTool(View.Tool);
        state.ProjectReplaced += () => _snapButton.Content = SnapLabel();
    }

    /// <summary>The 3D view; the main window drives its scene and camera.</summary>
    public StageView View { get; }

    /// <summary>Jumps to a named view (keys 1–5).</summary>
    public void SetPreset(ViewPreset preset) => View.ApplyPreset(preset);

    /// <summary>Switches tool and lights its button (Q, W, A).</summary>
    public void SetTool(BuilderTool tool)
    {
        View.Tool = tool;
        foreach (var (key, button) in _tools)
            button.IsChecked = key == tool;
    }

    private ToggleButton Tool(BuilderTool tool, string glyph, string tip)
    {
        var button = new ToggleButton { Content = glyph, Width = 30, MinHeight = 24, Padding = new Thickness(0), FontSize = 14, HorizontalContentAlignment = HorizontalAlignment.Center };
        ToolTip.SetTip(button, tip);
        button.Click += (_, _) => SetTool(tool);
        _tools[tool] = button;
        return button;
    }

    private static Control Divider() => new Border { Width = 1, Height = 18, Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3E)), Margin = new Thickness(2, 0) };

    private string SnapLabel() => $"⊞ {_state.Project.Stage.GridStep:0.##} m";

    private MenuFlyout SnapMenu()
    {
        var menu = new MenuFlyout();
        foreach (var step in SnapSteps)
        {
            var item = new MenuItem { Header = $"{step:0.##} m" };
            item.Click += (_, _) =>
            {
                _state.Project.Stage.GridStep = step;
                _state.Commit();
                _snapButton.Content = SnapLabel();
            };
            menu.Items.Add(item);
        }
        return menu;
    }

    private MenuFlyout AddMenu()
    {
        var menu = new MenuFlyout();
        foreach (var (kind, label) in new[] { (StageObjectKind.Truss, "Truss"), (StageObjectKind.Platform, "Riser / platform"), (StageObjectKind.Wall, "Wall"), (StageObjectKind.Screen, "LED screen") })
        {
            var item = new MenuItem { Header = label };
            item.Click += (_, _) => AddObject(kind);
            menu.Items.Add(item);
        }
        return menu;
    }

    /// <summary>Blender's split gizmo button: the icon flips every gizmo at once, the arrow picks which kinds show.</summary>
    private Control GizmoToggle(ViewGizmo navigation)
    {
        var toggle = new ToggleButton { Content = "↗", IsChecked = View.ShowGizmos, Width = 28, MinHeight = 24, Padding = new Thickness(0), FontSize = 14, HorizontalContentAlignment = HorizontalAlignment.Center };
        ToolTip.SetTip(toggle, "Show gizmos");

        var kinds = new StackPanel { Spacing = 4, Width = 180 };
        kinds.Children.Add(new TextBlock { Text = "Viewport gizmos", Foreground = Brushes.Gray, Margin = new Thickness(0, 0, 0, 2) });
        kinds.Children.Add(GizmoCheck("Navigate", View.ShowNavigation, v => View.ShowNavigation = v));
        kinds.Children.Add(GizmoCheck("Move handles", View.ShowMoveHandles, v => View.ShowMoveHandles = v));
        kinds.Children.Add(GizmoCheck("Measurements", View.ShowMeasurements, v => View.ShowMeasurements = v));
        var dropdown = new Button { Content = "▾", Width = 18, MinHeight = 24, Padding = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Center, Flyout = new Flyout { Content = kinds, Placement = PlacementMode.BottomEdgeAlignedRight } };
        ToolTip.SetTip(dropdown, "Choose which gizmos show");

        void Apply()
        {
            View.ShowGizmos = toggle.IsChecked == true;
            navigation.IsVisible = View.ShowGizmos && View.ShowNavigation;
            // Greyed out rather than hidden, so you can still pick kinds before turning gizmos back on.
            kinds.Opacity = View.ShowGizmos ? 1 : 0.5;
        }
        toggle.IsCheckedChanged += (_, _) => Apply();
        foreach (var child in kinds.Children)
            if (child is CheckBox check)
                check.IsCheckedChanged += (_, _) => Apply();

        return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0, Children = { toggle, dropdown } };
    }

    private static CheckBox GizmoCheck(string label, bool isChecked, Action<bool> set)
    {
        var check = new CheckBox { Content = label, IsChecked = isChecked };
        check.IsCheckedChanged += (_, _) => set(check.IsChecked == true);
        return check;
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
        SetTool(BuilderTool.Move);
    }
}
