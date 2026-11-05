using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Lampion.App.Controls;
using Lampion.Core.Model.Stage;

namespace Lampion.App.Views.Inspector;

/// <summary>Effect Controls for a set piece (platform, truss, ...): kind, colour, transform, and quick actions.</summary>
/// <param name="fields">Editor factory wired to the inspector.</param>
internal sealed class StageObjectEditor(InspectorFields fields)
{
    private static readonly ScrubRange HorizontalRange = new(-200, 200, 0.01, Unit: " m");
    private static readonly ScrubRange HeightRange = new(-10, 100, 0.01, Unit: " m");
    private static readonly ScrubRange WidthRange = new(0.05, 200, 0.01, Unit: " m");
    private static readonly ScrubRange TallRange = new(0.02, 100, 0.01, Unit: " m");
    private static readonly ScrubRange DepthRange = new(0.02, 200, 0.01, Unit: " m");
    private static readonly ScrubRange YawRange = new(-360, 360, 0.5, Format: "0.#", Unit: "°");
    private static readonly ScrubRange TiltRange = new(-180, 180, 0.5, Format: "0.#", Unit: "°");

    private readonly StructureSection _structure = new(fields);

    /// <summary>Adds the sections for <paramref name="obj"/> to <paramref name="root"/>.</summary>
    public void Build(Panel root, StageObject obj)
    {
        var context = fields.Context;
        InspectorLayout.Title(root, obj.Name, $"{obj.Kind} · top at {obj.Position.Y + StageMath.HalfHeight(obj):0.00} m");
        if (StructureEdits.Find(context.State.Project, obj.StructureId) is { } structure)
            _structure.Build(root, structure);
        var section = InspectorLayout.Section(root, "object", "Object");
        InspectorLayout.Row(section, "Name", fields.Text(obj.Name, v => obj.Name = v));
        InspectorLayout.Row(section, "Kind", fields.Choice(Enum.GetValues<StageObjectKind>(), obj.Kind, v => obj.Kind = v));
        InspectorLayout.Row(section, "Colour", fields.Color(obj.Color, hex => obj.Color = hex));

        var transform = InspectorLayout.Section(root, "object-transform", "Transform");
        InspectorLayout.Row(transform, "Position X", fields.Scrub(obj.Position.X, HorizontalRange, v => obj.Position = obj.Position with { X = Math.Round(v, 3) }));
        InspectorLayout.Row(transform, "Position Y", fields.Scrub(obj.Position.Y, HeightRange, v => obj.Position = obj.Position with { Y = Math.Round(v, 3) }), "Height of the centre");
        InspectorLayout.Row(transform, "Position Z", fields.Scrub(obj.Position.Z, HorizontalRange, v => obj.Position = obj.Position with { Z = Math.Round(v, 3) }));
        InspectorLayout.Row(transform, obj.Kind == StageObjectKind.Truss ? "Length" : "Width", fields.Scrub(obj.Size.X, WidthRange, v => obj.Size = obj.Size with { X = Math.Max(0.05, Math.Round(v, 3)) }));
        InspectorLayout.Row(transform, "Height", fields.Scrub(obj.Size.Y, TallRange, v => obj.Size = obj.Size with { Y = Math.Max(0.02, Math.Round(v, 3)) }));
        InspectorLayout.Row(transform, "Depth", fields.Scrub(obj.Size.Z, DepthRange, v => obj.Size = obj.Size with { Z = Math.Max(0.02, Math.Round(v, 3)) }));
        InspectorLayout.Row(transform, "Yaw", fields.Scrub(obj.Yaw, YawRange, v => obj.Yaw = Math.Round(v, 1)), "Turn around the vertical");
        InspectorLayout.Row(transform, "Pitch", fields.Scrub(obj.Pitch, TiltRange, v => obj.Pitch = Math.Round(v, 1)), "Tip the top toward (+) or away from the audience");
        InspectorLayout.Row(transform, "Roll", fields.Scrub(obj.Roll, TiltRange, v => obj.Roll = Math.Round(v, 1)), obj.Kind == StageObjectKind.Truss ? "90° stands the truss upright" : "Lean sideways");

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(20, 4, 0, 0) };
        if (obj.Kind == StageObjectKind.Platform)
            actions.Children.Add(Ui.Button("Sit on floor", () =>
            {
                context.Edit(() => obj.Position = obj.Position with { Y = StageMath.HalfHeight(obj) });
                context.Rebuild();
            }));
        if (obj.Kind == StageObjectKind.Truss)
            actions.Children.Add(Ui.Button(IsUpright(obj) ? "Lay flat" : "Stand upright", () =>
            {
                context.Edit(() => ToggleUpright(obj));
                context.Rebuild();
            }));
        actions.Children.Add(Ui.Button("Duplicate", () =>
        {
            var copy = obj.Clone();
            copy.Name += " copy";
            copy.Position += new Vec3(0.5, 0, 0);
            context.State.Project.Stage.Objects.Add(copy);
            context.State.Commit();
            context.State.SelectObject(copy.Id);
        }));
        transform.Body.Children.Add(actions);
    }

    private static bool IsUpright(StageObject truss) => truss.Pitch == 0 && Math.Abs(Math.Abs(truss.Roll) - 90) < 1;

    /// <summary>Swaps a truss between lying flat and standing on the floor as a tower, keeping its X and Z.</summary>
    private static void ToggleUpright(StageObject truss)
    {
        truss.Roll = IsUpright(truss) ? 0 : 90;
        truss.Pitch = 0;
        truss.Position = truss.Position with { Y = StageMath.HalfHeight(truss) };
    }
}
