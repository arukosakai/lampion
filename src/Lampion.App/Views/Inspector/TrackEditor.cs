using Avalonia;
using Avalonia.Controls;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Views.Inspector;

/// <summary>Effect Controls for a track: name, mute/solo, blend and fixture order, and (for light tracks) its target fixtures and groups.</summary>
/// <param name="fields">Editor factory wired to the inspector.</param>
internal sealed class TrackEditor(InspectorFields fields)
{
    /// <summary>Adds the sections for <paramref name="track"/> to <paramref name="root"/>.</summary>
    public void Build(Panel root, Track track)
    {
        InspectorLayout.Title(root, track.Name, $"{track.Kind} track");
        var section = InspectorLayout.Section(root, "track", "Track");
        InspectorLayout.Row(section, "Name", fields.Text(track.Name, v => track.Name = v));
        InspectorLayout.Row(section, "Muted", fields.Check(track.Muted, v => track.Muted = v));
        if (track.Kind != TrackKind.Light)
            return;
        InspectorLayout.Row(section, "Solo", fields.Check(track.Solo, v => track.Solo = v));
        InspectorLayout.Row(section, "Blend", fields.Choice(Enum.GetValues<BlendMode>(), track.Blend, v => track.Blend = v), "How this track combines with the tracks below it");
        InspectorLayout.Row(section, "Fixture order", fields.Choice(Enum.GetValues<FixtureOrder>(), track.Order, v => track.Order = v, o => DisplayNames.Humanize(o.ToString())), "The order chases, rainbow spread and wipes walk the fixtures");

        var targets = InspectorLayout.Section(root, "targets", "Targets");
        targets.Body.Children.Add(InspectorLayout.Hint("Tip: drag fixtures or groups from the Project panel onto the track.", new Thickness(20, 0, 0, 4)));
        var project = fields.Context.State.Project;
        foreach (var group in project.Groups)
            targets.Body.Children.Add(TargetCheck($"▣ {group.Name} ({group.FixtureIds.Count})", track.GroupIds, group.Id));
        foreach (var fixture in project.Fixtures)
            targets.Body.Children.Add(TargetCheck(fixture.Name, track.FixtureIds, fixture.Id));
    }

    private CheckBox TargetCheck(string label, List<string> ids, string id)
    {
        var box = new CheckBox { Content = label, IsChecked = ids.Contains(id), Margin = new Thickness(20, 0, 0, 0), MinHeight = 24 };
        box.IsCheckedChanged += (_, _) => fields.Context.Edit(() =>
        {
            ids.Remove(id);
            if (box.IsChecked == true)
                ids.Add(id);
        });
        return box;
    }
}
