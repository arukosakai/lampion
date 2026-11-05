using System.Xml.Linq;
using Lampion.Core.Model;

namespace Lampion.Core.Export;

/// <summary>
/// Places new fixtures in QLC+ 4's 2D fixture monitor, front view: x across, height measured down from
/// the top of the grid (the monitor's y grows downward). Positions are top-left corners in millimetres.
/// </summary>
internal static class QlcMonitorWriter
{
    private const int Item = 300;

    /// <summary>Adds FxItems for <paramref name="fixtures"/>, growing (never shrinking) the monitor grid to fit.</summary>
    public static void Write(QlcWorkspace workspace, Project project, List<QlcPatchedFixture> fixtures)
    {
        if (fixtures.Count == 0)
            return;
        var ns = workspace.Ns;
        var engine = workspace.Engine;
        var left = fixtures.Min(f => f.Fixture.Position.X * 1000 - Width(f) / 2);
        var right = fixtures.Max(f => f.Fixture.Position.X * 1000 + Width(f) / 2);
        var top = Math.Max(0, fixtures.Max(f => f.Fixture.Position.Y * 1000 + Item / 2.0));
        var monitor = engine.Element(ns + "Monitor");
        var grid = monitor?.Element(ns + "Grid");
        var existingWidth = QlcWorkspace.IntOr((string?)grid?.Attribute("Width"), 0) * 1000;
        var existingHeight = QlcWorkspace.IntOr((string?)grid?.Attribute("Depth") ?? (string?)grid?.Attribute("Height"), 0) * 1000;
        var gridWidth = Math.Max(existingWidth, Math.Ceiling(Math.Max(5000, right - left + 2000) / 1000) * 1000);
        var gridHeight = Math.Max(existingHeight, Math.Ceiling(Math.Max(3000, top + 1000) / 1000) * 1000);
        var origin = (left + right) / 2 - gridWidth / 2;

        if (monitor is null)
        {
            monitor = new XElement(ns + "Monitor", new XAttribute("DisplayMode", 1), new XAttribute("ShowLabels", 1), new XElement(ns + "ChannelStyle", 0), new XElement(ns + "ValueStyle", 0));
            engine.Add(monitor);
        }
        if (grid is null)
        {
            grid = new XElement(ns + "Grid", new XAttribute("Units", 0));
            monitor.Add(grid);
        }
        grid.SetAttributeValue("Width", (int)(gridWidth / 1000));
        grid.SetAttributeValue("Height", (int)(gridHeight / 1000));
        grid.SetAttributeValue("Depth", (int)(gridHeight / 1000));
        var groupColors = project.Groups.SelectMany(g => g.FixtureIds.Select(id => (id, g.Color))).GroupBy(x => x.id).ToDictionary(x => x.Key, x => x.First().Color);
        foreach (var f in fixtures)
        {
            var element = new XElement(
                ns + "FxItem",
                new XAttribute("ID", f.QlcId),
                new XAttribute("XPos", (int)Math.Round(f.Fixture.Position.X * 1000 - origin - Width(f) / 2)),
                new XAttribute("YPos", (int)Math.Round(gridHeight - f.Fixture.Position.Y * 1000 - Item / 2.0))
            );
            if (QlcFixtureWriter.RgbPanelMode(f.Profile) is null && groupColors.TryGetValue(f.Fixture.Id, out var gel))
                element.SetAttributeValue("GelColor", gel);
            monitor.Add(element);
        }
    }

    /// <summary>Millimetres the fixture takes up across: one cell for an RGB panel, one per channel for a dimmer pack.</summary>
    private static double Width(QlcPatchedFixture f) => QlcFixtureWriter.RgbPanelMode(f.Profile) is null ? Item * f.Profile.Channels.Count : Item;
}
