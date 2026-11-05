using Avalonia.Controls;
using Avalonia.Layout;
using Lampion.App.Controls;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Stage;

namespace Lampion.App.Views.Inspector;

/// <summary>The "Rig tools" section for selected fixtures: aim them at a point, or hang them along a truss.</summary>
/// <param name="context">The inspector the section belongs to.</param>
internal sealed class RigToolsSection(InspectorContext context)
{
    /// <summary>Adds the section to <paramref name="root"/>; the tools act on <paramref name="selected"/>.</summary>
    public void Build(Panel root, List<Fixture> selected)
    {
        var tools = InspectorLayout.Section(root, "rig-tools", "Rig tools", open: false);
        double ax = 0, ay = 1, az = 0;
        var aimPoint = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                AimScrub(ax, v => ax = v),
                AimScrub(ay, v => ay = v),
                AimScrub(az, v => az = v),
                new GlyphButton("Aim", "Point the selected fixtures at this X / Y / Z", () =>
                {
                    context.Edit(() => selected.ForEach(f => StageMath.AimAt(f, new Vec3(ax, ay, az))));
                    context.Rebuild();
                }),
            },
        };
        InspectorLayout.Row(tools, "Aim at", aimPoint, "Or use the Aim tool (A) in the stage view and click a spot");

        var trusses = context.State.Project.Stage.Objects.Where(o => o.Kind == StageObjectKind.Truss).ToList();
        if (trusses.Count == 0)
            return;
        var truss = trusses[0];
        var combo = InspectorFields.Compact(Ui.Combo(trusses, truss, t => truss = t, t => t.Name));
        var hang = new GlyphButton("Hang", "Spread the selected fixtures evenly under the truss", () =>
        {
            context.Edit(() => StageMath.DistributeAlong(truss, selected.OrderBy(f => f.Position.X).ToList()));
            context.Rebuild();
        });
        InspectorLayout.Row(tools, "Distribute on", new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { combo, hang } });
    }

    // The aim point is scratch input, not part of the project, so it bypasses the edit/undo path.
    private static ScrubValue AimScrub(double value, Action<double> set)
    {
        var scrub = new ScrubValue(value, -100, 100, 0.02, unit: " m");
        scrub.Changing += set;
        scrub.Committed += set;
        return scrub;
    }
}
