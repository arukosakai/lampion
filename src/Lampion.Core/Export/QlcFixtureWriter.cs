using System.Xml.Linq;
using Lampion.Core.Model.Rig;

namespace Lampion.Core.Export;

/// <summary>Chooses the QLC+ fixture definition for a profile and writes the Fixture element.</summary>
internal static class QlcFixtureWriter
{
    /// <summary>
    /// QLC+'s built-in RGB panel mode when the channels are exactly R, G, B in some order, or R, G, B, W —
    /// the monitor then mixes the colour. Null means a Generic dimmer pack.
    /// </summary>
    public static string? RgbPanelMode(FixtureProfile profile)
    {
        var roles = profile.Channels.Select(c => c.Role).ToList();
        if (roles.Count == 3 && roles.Contains(ChannelRole.Red) && roles.Contains(ChannelRole.Green) && roles.Contains(ChannelRole.Blue))
            return string.Concat(roles.Select(r => r.ToString()[0]));
        if (roles.SequenceEqual([ChannelRole.Red, ChannelRole.Green, ChannelRole.Blue, ChannelRole.White]))
            return "RGBW";
        return null;
    }

    /// <summary>The Fixture element for a newly added fixture (QLC+ universes and addresses are 0-based).</summary>
    public static XElement Element(XNamespace ns, QlcPatchedFixture patched)
    {
        var element = new XElement(ns + "Fixture", new XElement(ns + "Manufacturer", "Generic"));
        if (RgbPanelMode(patched.Profile) is { } mode)
            element.Add(new XElement(ns + "Model", "RGBPanel"), new XElement(ns + "Mode", mode), new XElement(ns + "Width", 300), new XElement(ns + "Height", 300));
        else
            element.Add(new XElement(ns + "Model", "Generic"), new XElement(ns + "Mode", $"{patched.Profile.Channels.Count} Channel"));
        element.Add(
            new XElement(ns + "ID", patched.QlcId),
            new XElement(ns + "Name", patched.Fixture.Name),
            new XElement(ns + "Universe", patched.Fixture.Universe - 1),
            new XElement(ns + "Address", patched.Fixture.Address - 1),
            new XElement(ns + "Channels", patched.Profile.Channels.Count)
        );
        return element;
    }
}
