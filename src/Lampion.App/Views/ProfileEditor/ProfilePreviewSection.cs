using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Lampion.App.Controls;

namespace Lampion.App.Views.ProfileEditor;

/// <summary>
/// The live 3D preview of the profile being edited, with its Orbit / ◎ Aim switch. Shows the session's test
/// values and, in aim mode, turns clicks on the set into pan/tilt for <see cref="Aimed"/>.
/// </summary>
internal sealed class ProfilePreviewSection
{
    private readonly ProfileEditSession _session;
    private readonly FixturePreview _preview;
    private readonly ToggleButton _orbit = new() { Content = "Orbit", IsChecked = true, Padding = new Thickness(10, 2), CornerRadius = new CornerRadius(3, 0, 0, 3) };
    private readonly ToggleButton _aim = new() { Content = "◎ Aim", Padding = new Thickness(10, 2), CornerRadius = new CornerRadius(0, 3, 3, 0) };
    private readonly StackPanel _modeSwitch;
    private readonly TextBlock _hint = new() { Foreground = ProfileEditorUi.Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };

    /// <param name="session">The editor session whose working profile and test values are drawn.</param>
    public ProfilePreviewSection(ProfileEditSession session)
    {
        _session = session;
        _preview = new FixturePreview { Profile = session.Working };
        ToolTip.SetTip(_orbit, "Drag to orbit the camera");
        ToolTip.SetTip(_aim, "Click or drag on the floor or wall to point the head there (right-drag still orbits)");
        _modeSwitch = new StackPanel { Orientation = Orientation.Horizontal, Children = { _orbit, _aim }, VerticalAlignment = VerticalAlignment.Center };
        _orbit.Click += (_, _) => SetAim(false);
        _aim.Click += (_, _) => SetAim(true);
        SetAim(false);

        var right = new StackPanel { Orientation = Orientation.Horizontal, Children = { _modeSwitch, _hint } };
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(right, Dock.Right);
        header.Children.Add(right);
        header.Children.Add(new TextBlock { Text = "Preview", FontWeight = FontWeight.SemiBold, FontSize = 14, VerticalAlignment = VerticalAlignment.Center });
        Header = header;
        View = new Border { CornerRadius = new CornerRadius(4), ClipToBounds = true, Child = _preview, MinHeight = 260 };

        session.ChannelsChanged += Refresh;
        session.ValuesChanged += () => _preview.SetValues(session.Test.Span);
    }

    /// <summary>The "Preview" title with the mode switch and hint, for docking above <see cref="View"/>.</summary>
    public Control Header { get; }

    /// <summary>The framed 3D view; it takes whatever height is left.</summary>
    public Control View { get; }

    /// <summary>Raised with the pan/tilt (0..1) that point the head at a clicked spot in aim mode.</summary>
    public event Action<double, double>? Aimed
    {
        add => _preview.Aimed += value;
        remove => _preview.Aimed -= value;
    }

    private void Refresh()
    {
        var moves = _session.Working.HasPanTilt;
        _modeSwitch.IsVisible = moves;
        if (!moves)
            SetAim(false);
    }

    private void SetAim(bool on)
    {
        _preview.AimMode = on;
        _orbit.IsChecked = !on;
        _aim.IsChecked = on;
        _hint.Text = on ? "click the set to aim · right-drag orbits" : "drag to orbit · scroll to zoom";
    }
}
