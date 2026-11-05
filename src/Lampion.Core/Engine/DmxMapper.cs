using Lampion.Core.Color;
using Lampion.Core.Model;

namespace Lampion.Core.Engine;

/// <summary>Turns a <see cref="FixtureState"/> into channel values for one fixture's profile, and back for the preview.</summary>
public static class DmxMapper
{
    /// <summary>Channel values for the fixture's whole footprint, in profile order.</summary>
    public static byte[] Map(Fixture fixture, FixtureProfile profile, FixtureState state)
    {
        var channels = profile.Channels;
        var values = new byte[channels.Count];
        for (var i = 0; i < channels.Count; i++)
            values[i] = channels[i].Default;

        var light = state.Light.Clamped();
        var hasDimmer = profile.Has(ChannelRole.Dimmer);
        var hasWhite = profile.Has(ChannelRole.White);
        var white = state.White is { } w ? Rgb.Clamp01(w) : 0.0;

        if (profile.HasColor)
        {
            if (state.White is null && hasWhite && fixture.ExtractWhite)
            {
                // The part all three share is white light; a real white LED renders it better than RGB.
                white = light.Min;
                light = new Rgb(light.R - white, light.G - white, light.B - white);
            }
            var level = hasDimmer ? Math.Max(light.Max, hasWhite ? white : 0) : 1;
            var normalize = level > 1e-9 ? 1 / level : 0;
            Set(values, profile, ChannelRole.Red, light.R * normalize);
            Set(values, profile, ChannelRole.Green, light.G * normalize);
            Set(values, profile, ChannelRole.Blue, light.B * normalize);
            Set(values, profile, ChannelRole.White, white * normalize);
            Set(values, profile, ChannelRole.Dimmer, level);
        }
        else
        {
            // No colour mixing: the fixture's brightness is the brightest thing asked of it.
            var level = Math.Max(light.Max, white);
            if (hasDimmer)
                Set(values, profile, ChannelRole.Dimmer, level);
            else
                Set(values, profile, ChannelRole.White, level);
        }

        if (state.Roles is not null)
            foreach (var (role, value) in state.Roles)
                SetRoleValue(values, profile, role, value);

        if (state.Raw is not null)
            foreach (var (offset, value) in state.Raw)
                if (offset >= 0 && offset < values.Length)
                    values[offset] = Rgb.ToByte(value);
        return values;
    }

    /// <summary>Writes a 0..1 role value, using the fine channel for 16-bit pan/tilt when the profile has one.</summary>
    public static void SetRoleValue(byte[] values, FixtureProfile profile, ChannelRole role, double value)
    {
        var fineRole = role switch
        {
            ChannelRole.Pan => ChannelRole.PanFine,
            ChannelRole.Tilt => ChannelRole.TiltFine,
            _ => (ChannelRole?)null,
        };
        var coarse = profile.IndexOf(role);
        if (coarse < 0)
            return;
        var fine = fineRole is { } fr ? profile.IndexOf(fr) : -1;
        if (fine < 0)
        {
            values[coarse] = Rgb.ToByte(value);
            return;
        }
        var wide = (int)Math.Round(Rgb.Clamp01(value) * 65535);
        values[coarse] = (byte)(wide >> 8);
        values[fine] = (byte)(wide & 0xFF);
    }

    private static void Set(byte[] values, FixtureProfile profile, ChannelRole role, double value)
    {
        var index = profile.IndexOf(role);
        if (index >= 0)
            values[index] = Rgb.ToByte(value);
    }

    /// <summary>
    /// Reads channel values back into what the fixture visibly emits. The 3D preview is drawn from this,
    /// so it shows the DMX Lampion actually sends rather than the intent behind it.
    /// </summary>
    public static EmittedLight Decode(Fixture fixture, FixtureProfile profile, ReadOnlySpan<byte> channelValues)
    {
        var values = channelValues.ToArray();
        double Read(ChannelRole role, double fallback)
        {
            var i = profile.IndexOf(role);
            return i >= 0 && i < values.Length ? values[i] / 255.0 : fallback;
        }
        double Wide(ChannelRole coarse, ChannelRole fine)
        {
            var c = profile.IndexOf(coarse);
            if (c < 0 || c >= values.Length)
                return 0.5;
            var f = profile.IndexOf(fine);
            return f >= 0 && f < values.Length ? (values[c] * 256 + values[f]) / 65535.0 : values[c] / 255.0;
        }

        var dimmer = Read(ChannelRole.Dimmer, 1);
        Rgb color;
        if (profile.HasColor)
        {
            color = new Rgb(Read(ChannelRole.Red, 0), Read(ChannelRole.Green, 0), Read(ChannelRole.Blue, 0));
            var white = Read(ChannelRole.White, 0);
            var amber = Read(ChannelRole.Amber, 0);
            var uv = Read(ChannelRole.UV, 0);
            color += new Rgb(white, white, white * 0.92) + new Rgb(amber, amber * 0.55, 0) + new Rgb(uv * 0.35, 0, uv);
        }
        else
        {
            var level = profile.Has(ChannelRole.Dimmer) ? 1 : Read(ChannelRole.White, 0);
            color = Rgb.FromHex(fixture.GelColor) * level;
        }
        return new EmittedLight(
            (color * dimmer).Clamped(),
            Wide(ChannelRole.Pan, ChannelRole.PanFine),
            Wide(ChannelRole.Tilt, ChannelRole.TiltFine),
            Read(ChannelRole.Strobe, 0),
            Read(ChannelRole.Zoom, 0.5)
        );
    }
}

/// <summary>What the preview draws for one fixture. Pan/tilt are 0..1 with 0.5 at centre.</summary>
public readonly record struct EmittedLight(Rgb Color, double Pan, double Tilt, double Strobe, double Zoom);
