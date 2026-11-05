using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Lampion.App.Controls;
using Lampion.Core.Model.Stage;

namespace Lampion.App.Views.Inspector;

/// <summary>
/// Effect Controls for the structure a set piece belongs to: its name, where it stands and how it's turned
/// (pieces and hung fixtures move together), plus ungroup and delete.
/// </summary>
/// <param name="fields">Editor factory wired to the inspector.</param>
internal sealed class StructureSection(InspectorFields fields)
{
    private static readonly ScrubRange PlanRange = new(-200, 200, 0.01, Unit: " m");
    private static readonly ScrubRange TurnRange = new(-360, 360, 0.5, Format: "0.#", Unit: "°");

    /// <summary>Adds the section for <paramref name="structure"/> to <paramref name="root"/>.</summary>
    public void Build(Panel root, Structure structure)
    {
        var context = fields.Context;
        var project = context.State.Project;
        var id = structure.Id;
        var pieces = StructureEdits.Pieces(project, id).Count();
        var lights = StructureEdits.Fixtures(project, id).Count();
        var centre = StructureEdits.Centre(project, id);

        var section = InspectorLayout.Section(root, "structure", "Structure");
        InspectorLayout.Row(section, "Name", fields.Text(structure.Name, v => structure.Name = v));
        InspectorLayout.Row(section, "Centre X", fields.Scrub(centre.X, PlanRange, v => StructureEdits.MoveBy(project, id, new Vec3(v - StructureEdits.Centre(project, id).X, 0, 0))), "Moves every piece and hung light");
        InspectorLayout.Row(section, "Centre Z", fields.Scrub(centre.Z, PlanRange, v => StructureEdits.MoveBy(project, id, new Vec3(0, 0, v - StructureEdits.Centre(project, id).Z))), "Toward the audience is +");
        InspectorLayout.Row(section, "Turn", fields.Scrub(structure.Yaw, TurnRange, v => StructureEdits.Turn(project, id, v - structure.Yaw)), "Turns it around its centre, lights included");

        var summary = Ui.Label($"{pieces} pieces · {lights} lights hung · Ctrl+drag moves one piece");
        summary.Margin = new Thickness(20, 2, 0, 0);
        summary.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        section.Body.Children.Add(summary);

        var actions = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(20, 4, 0, 0) };
        actions.Children.Add(ActionButton("Ungroup", "Keep the pieces and lights where they are, but move them separately", deselect: false, () => StructureEdits.Ungroup(project, id)));
        actions.Children.Add(ActionButton("Delete", "Remove the truss pieces; the lights stay patched where they hang", deselect: true, () => StructureEdits.Delete(project, id, withFixtures: false)));
        if (lights > 0)
            actions.Children.Add(ActionButton("Delete with lights", "Remove the truss pieces and unpatch the lights hung on them", deselect: true, () => StructureEdits.Delete(project, id, withFixtures: true)));
        section.Body.Children.Add(actions);
    }

    private Button ActionButton(string label, string tip, bool deselect, Action change)
    {
        var button = Ui.Button(label, () =>
        {
            var context = fields.Context;
            context.Edit(change);
            if (deselect)
                context.State.Deselect();
            context.Rebuild();
        }, tip);
        button.Margin = new Thickness(0, 0, 6, 4);
        return button;
    }
}
