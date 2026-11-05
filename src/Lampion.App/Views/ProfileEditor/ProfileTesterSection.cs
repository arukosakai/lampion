using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Lampion.App.Editor;

namespace Lampion.App.Views.ProfileEditor;

/// <summary>
/// The Tester under the preview: the output picker, the console-style <see cref="FixtureTester"/> and the
/// Full on / Defaults / Blackout presets.
/// </summary>
internal sealed class ProfileTesterSection : StackPanel
{
    private readonly FixtureTester _tester;

    /// <param name="state">Receives the held values when testing on the rig.</param>
    /// <param name="session">The editor session whose test values are edited.</param>
    public ProfileTesterSection(EditorState state, ProfileEditSession session)
    {
        Spacing = 8;
        _tester = new FixtureTester(session.Working, session.Test, session.NotifyValuesChanged);

        var title = ProfileEditorUi.Heading("Tester");
        title.Margin = new Thickness(0);
        title.VerticalAlignment = VerticalAlignment.Center;
        var output = new TestOutputPicker(state, session);
        var header = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
        DockPanel.SetDock(output, Dock.Right);
        header.Children.Add(output);
        header.Children.Add(title);

        var presets = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                Ui.Button("Full on", () => session.SetAllTestValues(ProfileEditSession.FullOn), "Dimmer and colour at full, everything else at rest"),
                Ui.Button("Defaults", () => session.SetAllTestValues(c => c.Default), "What the fixture shows when nothing drives it"),
                Ui.Button("Blackout", () => session.SetAllTestValues(_ => 0)),
            },
        };

        Children.Add(header);
        Children.Add(_tester);
        Children.Add(presets);

        session.ValuesChanged += _tester.Sync;
    }

    /// <summary>Points the head, as aiming in the 3D preview does; pan and tilt are 0..1.</summary>
    public void SetPanTilt(double pan, double tilt) => _tester.SetPanTilt(pan, tilt);
}
