using Avalonia.Controls;
using Avalonia.Layout;
using Lampion.App.Controls;
using Lampion.Core.Model.Rig;

namespace Lampion.App.Views.Inspector;

/// <summary>
/// Effect Controls for patched fixtures: patch (one fixture), transform, rig tools and live DMX output.
/// With several selected, the shared properties edit all of them.
/// </summary>
internal sealed class FixtureEditor
{
    private static readonly ScrubRange UniverseRange = new(1, 64, 0.1, Format: "0");
    private static readonly ScrubRange AddressRange = new(1, 512, 0.25, Format: "0");
    private static readonly ScrubRange HorizontalRange = new(-200, 200, 0.01, Unit: " m");
    private static readonly ScrubRange HeightRange = new(-10, 100, 0.01, Unit: " m");
    private static readonly ScrubRange YawRange = new(-360, 360, 0.5, Format: "0.#", Unit: "°");
    private static readonly ScrubRange PitchRange = new(-180, 90, 0.5, Format: "0.#", Unit: "°");
    private static readonly ScrubRange RollRange = new(-180, 180, 0.5, Format: "0.#", Unit: "°");

    private readonly InspectorFields _fields;
    private readonly RigToolsSection _rigTools;

    /// <summary>Creates the editor and its section components.</summary>
    /// <param name="fields">Editor factory wired to the inspector.</param>
    public FixtureEditor(InspectorFields fields)
    {
        _fields = fields;
        _rigTools = new RigToolsSection(fields.Context);
    }

    /// <summary>Adds the sections for the fixture selection to <paramref name="root"/>; <paramref name="primary"/> supplies the values shown.</summary>
    public void Build(Panel root, Fixture primary)
    {
        var context = _fields.Context;
        var project = context.State.Project;
        var selected = context.State.SelectedFixtureIds.Select(project.FixtureById).OfType<Fixture>().ToList();
        var profile = project.ProfileOf(primary);
        var single = selected.Count == 1;
        InspectorLayout.Title(
            root,
            single ? primary.Name : $"{selected.Count} fixtures",
            single ? $"{profile?.DisplayName} · DMX {primary.Universe}.{primary.Address:000}" : "Edits apply to all of them"
        );

        if (single)
            BuildPatch(root, primary, profile);

        var transform = InspectorLayout.Section(root, "transform", "Transform");
        if (single)
        {
            InspectorLayout.Row(transform, "Position X", _fields.Scrub(primary.Position.X, HorizontalRange, v => primary.Position = primary.Position with { X = Math.Round(v, 3) }));
            InspectorLayout.Row(transform, "Position Y", _fields.Scrub(primary.Position.Y, HeightRange, v => primary.Position = primary.Position with { Y = Math.Round(v, 3) }), "Height above the floor");
            InspectorLayout.Row(transform, "Position Z", _fields.Scrub(primary.Position.Z, HorizontalRange, v => primary.Position = primary.Position with { Z = Math.Round(v, 3) }), "Toward the audience is +");
        }
        else
            InspectorLayout.Row(transform, "Height (all)", _fields.Scrub(primary.Position.Y, HeightRange, v => selected.ForEach(f => f.Position = f.Position with { Y = Math.Round(v, 3) })));
        InspectorLayout.Row(transform, "Yaw", _fields.Scrub(primary.Yaw, YawRange, v => selected.ForEach(f => f.Yaw = Math.Round(v, 1))), "0° faces the audience");
        InspectorLayout.Row(transform, "Pitch", _fields.Scrub(primary.Pitch, PitchRange, v => selected.ForEach(f => f.Pitch = Math.Round(v, 1))), "-90° points straight down");
        InspectorLayout.Row(transform, "Roll", _fields.Scrub(primary.Roll, RollRange, v => selected.ForEach(f => f.Roll = Math.Round(v, 1))), "Spins the fixture around its beam: 90° side-mounts a moving head or stands a batten upright");

        _rigTools.Build(root, selected);

        if (single && profile is not null)
        {
            var dmx = InspectorLayout.Section(root, "dmx", "DMX output", open: false);
            dmx.Body.Children.Add(new DmxReadout(context, primary, profile).View);
        }
    }

    private void BuildPatch(Panel root, Fixture fixture, FixtureProfile? profile)
    {
        var context = _fields.Context;
        var project = context.State.Project;
        var patch = InspectorLayout.Section(root, "patch", "Patch");
        InspectorLayout.Row(patch, "Name", _fields.Text(fixture.Name, v => fixture.Name = v));
        InspectorLayout.Row(patch, "Profile", _fields.Choice(project.Profiles, profile!, p => fixture.ProfileId = p.Id, p => p.DisplayName));
        InspectorLayout.Row(patch, "Universe", _fields.Scrub(fixture.Universe, UniverseRange, v => fixture.Universe = (int)Math.Round(v)));
        var address = _fields.Scrub(fixture.Address, AddressRange, v => fixture.Address = (int)Math.Round(v));
        var next = new GlyphButton("Next free", "Move to the first free address in this universe", () =>
        {
            var free = PatchPlanner.NextFreeAddress(project, fixture.Universe, profile?.Channels.Count ?? 1, fixture.Id);
            if (free > 0)
                context.Edit(() => fixture.Address = free);
            context.Rebuild();
        });
        InspectorLayout.Row(patch, "Address", new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { address, next } });
        if (profile?.Has(ChannelRole.White) == true)
            InspectorLayout.Row(patch, "Auto white", _fields.Check(fixture.ExtractWhite, v => fixture.ExtractWhite = v), "Moves the shared part of R/G/B onto the white LED");
        if (profile is { HasColor: false })
            InspectorLayout.Row(patch, "Gel", _fields.Color(fixture.GelColor, hex => fixture.GelColor = hex));
    }
}
