using Avalonia.Controls;
using Lampion.Core.Model.Rig;

namespace Lampion.App.Views.ProfileEditor;

/// <summary>
/// The Identity (manufacturer, model, mode) and Optics (look, beam angle, pan and tilt range) fields of the
/// profile editor. The ranges are disabled while the profile has no pan or tilt channel.
/// </summary>
internal sealed class ProfileIdentitySection : StackPanel
{
    private readonly ProfileEditSession _session;
    private readonly NumericUpDown _panRange;
    private readonly NumericUpDown _tiltRange;

    /// <param name="session">The editor session whose working profile is edited.</param>
    public ProfileIdentitySection(ProfileEditSession session)
    {
        _session = session;
        var working = session.Working;
        Spacing = 6;

        var identity = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 10 };
        ProfileEditorUi.Place(identity, ProfileEditorUi.Labelled("Manufacturer", ProfileEditorUi.LiveText(working.Manufacturer, v => working.Manufacturer = v)), 0);
        ProfileEditorUi.Place(identity, ProfileEditorUi.Labelled("Model", ProfileEditorUi.LiveText(working.Model, v => working.Model = v)), 1);
        ProfileEditorUi.Place(identity, ProfileEditorUi.Labelled("Mode", ProfileEditorUi.LiveText(working.Mode, v => working.Mode = v)), 2);

        _panRange = ProfileEditorUi.Plain(Ui.Number(working.PanRange, v => working.PanRange = v, 0, 720, 10, "0"));
        _tiltRange = ProfileEditorUi.Plain(Ui.Number(working.TiltRange, v => working.TiltRange = v, 0, 360, 10, "0"));
        var look = Ui.Combo(Enum.GetValues<BeamKind>(), working.Beam, v => working.Beam = v);
        var beamAngle = ProfileEditorUi.Plain(Ui.Number(working.BeamAngle, v => working.BeamAngle = v, 1, 180, 1, "0.#"));
        var optics = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*"), ColumnSpacing = 10 };
        ProfileEditorUi.Place(optics, ProfileEditorUi.Labelled("Look in 3D", look), 0);
        ProfileEditorUi.Place(optics, ProfileEditorUi.Labelled("Beam angle °", beamAngle), 1);
        ProfileEditorUi.Place(optics, ProfileEditorUi.Labelled("Pan range °", _panRange), 2);
        ProfileEditorUi.Place(optics, ProfileEditorUi.Labelled("Tilt range °", _tiltRange), 3);

        Children.Add(ProfileEditorUi.Heading("Identity"));
        Children.Add(identity);
        Children.Add(ProfileEditorUi.Heading("Optics"));
        Children.Add(optics);

        session.ChannelsChanged += Refresh;
    }

    private void Refresh() => _panRange.IsEnabled = _tiltRange.IsEnabled = _session.Working.HasPanTilt;
}
