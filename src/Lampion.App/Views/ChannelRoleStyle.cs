using Avalonia.Media;
using Lampion.Core.Model.Rig;

namespace Lampion.App.Views;

/// <summary>How each channel role is shown in the UI: a colour for its chip and a readable name.</summary>
internal static class ChannelRoleStyle
{
    private static readonly IBrush[] RoleBrushes = BuildBrushes();

    /// <summary>The chip colour for <paramref name="role"/>; unknown roles are grey.</summary>
    public static Color ColorOf(ChannelRole role) =>
        role switch
        {
            ChannelRole.Dimmer => Color.FromRgb(0xE8, 0xDF, 0xC8),
            ChannelRole.Red => Color.FromRgb(0xFF, 0x4D, 0x4D),
            ChannelRole.Green => Color.FromRgb(0x4C, 0xD9, 0x64),
            ChannelRole.Blue => Color.FromRgb(0x3D, 0x8B, 0xFF),
            ChannelRole.White => Color.FromRgb(0xF5, 0xF5, 0xF5),
            ChannelRole.Amber => Color.FromRgb(0xFF, 0xB0, 0x20),
            ChannelRole.UV => Color.FromRgb(0xA0, 0x5C, 0xFF),
            ChannelRole.Strobe => Color.FromRgb(0xFF, 0xE8, 0x5C),
            ChannelRole.Pan or ChannelRole.Tilt => Color.FromRgb(0x2E, 0xC4, 0xB6),
            ChannelRole.PanFine or ChannelRole.TiltFine => Color.FromRgb(0x1D, 0x80, 0x77),
            ChannelRole.PanTiltSpeed => Color.FromRgb(0x5F, 0x9E, 0x98),
            ChannelRole.Zoom => Color.FromRgb(0xFF, 0x8A, 0x5C),
            ChannelRole.ColorWheel => Color.FromRgb(0xFF, 0x5C, 0xA8),
            ChannelRole.Gobo => Color.FromRgb(0xC9, 0x9B, 0x6B),
            ChannelRole.Effect => Color.FromRgb(0xC7, 0x7D, 0xFF),
            _ => Color.FromRgb(0x8A, 0x8A, 0x8A),
        };

    /// <summary>A brush of <see cref="ColorOf"/>. Shared per role, so don't mutate it.</summary>
    public static IBrush BrushOf(ChannelRole role)
    {
        var index = (int)role;
        return (uint)index < (uint)RoleBrushes.Length ? RoleBrushes[index] : new SolidColorBrush(ColorOf(role));
    }

    /// <summary>The role's name as shown in lists and tooltips, e.g. "Pan/tilt speed".</summary>
    public static string NameOf(ChannelRole role) =>
        role switch
        {
            ChannelRole.PanFine => "Pan fine",
            ChannelRole.TiltFine => "Tilt fine",
            ChannelRole.PanTiltSpeed => "Pan/tilt speed",
            ChannelRole.ColorWheel => "Colour wheel",
            _ => role.ToString(),
        };

    /// <summary>Roles Lampion drives from colour on its own; the rest need a lane in Effect Controls.</summary>
    public static bool IsAutomatic(ChannelRole role) => role is ChannelRole.Dimmer or ChannelRole.Red or ChannelRole.Green or ChannelRole.Blue or ChannelRole.White;

    /// <summary>A sensible name for a fresh channel of this role, so picking a role can fill the name in.</summary>
    public static string DefaultName(ChannelRole role) => role == ChannelRole.Generic ? "Channel" : NameOf(role);

    private static IBrush[] BuildBrushes()
    {
        var roles = Enum.GetValues<ChannelRole>();
        var brushes = new IBrush[roles.Max(r => (int)r) + 1];
        for (var i = 0; i < brushes.Length; i++)
            brushes[i] = new SolidColorBrush(ColorOf((ChannelRole)i));
        return brushes;
    }
}
