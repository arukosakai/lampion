using Lampion.Core.Color;
using Lampion.Core.Model.Rig;

namespace Lampion.Core.Engine;

/// <summary>
/// Turns a <see cref="FixtureState"/> into channel values for one fixture's profile, and back for the
/// preview. Stateless, thread-safe and allocation-free.
/// </summary>
public static class DmxMapper
{
    /// <summary>Writes the fixture's whole footprint, in profile order, into the start of <paramref name="values"/>.</summary>
    /// <param name="fixture">The fixture (for its white-extraction setting).</param>
    /// <param name="profile">The fixture's profile.</param>
    /// <param name="state">What the fixture should do.</param>
    /// <param name="values">Receives <c>profile.Channels.Count</c> bytes; anything after them is left untouched.</param>
    /// <exception cref="ArgumentException"><paramref name="values"/> is shorter than the footprint.</exception>
    public static void Map(Fixture fixture, FixtureProfile profile, in FixtureState state, Span<byte> values)
    {
        var layout = ChannelLayout.Of(profile);
        Map(fixture, profile, in layout, in state, values);
    }

    /// <summary>
    /// <see cref="Map(Fixture, FixtureProfile, in FixtureState, Span{byte})"/> with the profile's layout already
    /// worked out, for callers mapping many fixtures per frame.
    /// </summary>
    /// <param name="fixture">The fixture (for its white-extraction setting).</param>
    /// <param name="profile">The fixture's profile.</param>
    /// <param name="layout"><see cref="ChannelLayout.Of"/> of <paramref name="profile"/>, built since its channels last changed.</param>
    /// <param name="state">What the fixture should do.</param>
    /// <param name="values">Receives <c>profile.Channels.Count</c> bytes; anything after them is left untouched.</param>
    /// <exception cref="ArgumentException"><paramref name="values"/> is shorter than the footprint.</exception>
    public static void Map(Fixture fixture, FixtureProfile profile, in ChannelLayout layout, in FixtureState state, Span<byte> values)
    {
        var channels = profile.Channels;
        if (values.Length < channels.Count)
            throw new ArgumentException($"Needs room for {channels.Count} channels, got {values.Length}.", nameof(values));
        values = values[..channels.Count];
        for (var i = 0; i < channels.Count; i++)
            values[i] = channels[i].Default;

        var light = state.Light.Clamped();
        var hasDimmer = layout.Has(ChannelRole.Dimmer);
        var hasWhite = layout.Has(ChannelRole.White);
        var white = state.White is { } w ? Rgb.Clamp01(w) : 0.0;

        if (layout.HasColor)
        {
            if (state.White is null && hasWhite && fixture.ExtractWhite)
            {
                // The part all three share is white light; a real white LED renders it better than RGB.
                white = light.Min;
                light = new Rgb(light.R - white, light.G - white, light.B - white);
            }
            var level = hasDimmer ? Math.Max(light.Max, hasWhite ? white : 0) : 1;
            var normalize = level > 1e-9 ? 1 / level : 0;
            Set(values, in layout, ChannelRole.Red, light.R * normalize);
            Set(values, in layout, ChannelRole.Green, light.G * normalize);
            Set(values, in layout, ChannelRole.Blue, light.B * normalize);
            Set(values, in layout, ChannelRole.White, white * normalize);
            Set(values, in layout, ChannelRole.Dimmer, level);
        }
        else
        {
            // No colour mixing: the fixture's brightness is the brightest thing asked of it.
            var level = Math.Max(light.Max, white);
            if (hasDimmer)
                Set(values, in layout, ChannelRole.Dimmer, level);
            else
                Set(values, in layout, ChannelRole.White, level);
        }

        for (var i = 0; i < state.RoleCount; i++)
        {
            var role = state.RoleAt(i);
            state.TryGetRole(role, out var value);
            SetRoleValue(values, in layout, role, value);
        }

        for (var i = 0; i < state.RawCount; i++)
        {
            var (offset, value) = state.RawAt(i);
            if (offset >= 0 && offset < values.Length)
                values[offset] = Rgb.ToByte(value);
        }
    }

    /// <summary>Writes a 0..1 role value, using the fine channel for 16-bit pan/tilt when the profile has one.</summary>
    internal static void SetRoleValue(Span<byte> values, in ChannelLayout layout, ChannelRole role, double value)
    {
        var fineRole = role switch
        {
            ChannelRole.Pan => ChannelRole.PanFine,
            ChannelRole.Tilt => ChannelRole.TiltFine,
            _ => (ChannelRole?)null,
        };
        var coarse = layout.IndexOf(role);
        if (coarse < 0)
            return;
        var fine = fineRole is { } fr ? layout.IndexOf(fr) : -1;
        if (fine < 0)
        {
            values[coarse] = Rgb.ToByte(value);
            return;
        }
        var wide = (int)Math.Round(Rgb.Clamp01(value) * 65535);
        values[coarse] = (byte)(wide >> 8);
        values[fine] = (byte)(wide & 0xFF);
    }

    private static void Set(Span<byte> values, in ChannelLayout layout, ChannelRole role, double value)
    {
        var index = layout.IndexOf(role);
        if (index >= 0)
            values[index] = Rgb.ToByte(value);
    }

    /// <summary>
    /// Reads channel values back into what the fixture visibly emits. The 3D preview is drawn from this,
    /// so it shows the DMX Lampion actually sends rather than the intent behind it.
    /// </summary>
    /// <param name="fixture">The fixture (for its gel colour).</param>
    /// <param name="profile">The fixture's profile.</param>
    /// <param name="channelValues">The fixture's channels in profile order; may be shorter than the footprint (missing channels read as absent).</param>
    public static EmittedLight Decode(Fixture fixture, FixtureProfile profile, ReadOnlySpan<byte> channelValues)
    {
        var layout = ChannelLayout.Of(profile);
        var dimmer = Read(channelValues, in layout, ChannelRole.Dimmer, 1);
        Rgb color;
        if (layout.HasColor)
        {
            color = new Rgb(Read(channelValues, in layout, ChannelRole.Red, 0), Read(channelValues, in layout, ChannelRole.Green, 0), Read(channelValues, in layout, ChannelRole.Blue, 0));
            var white = Read(channelValues, in layout, ChannelRole.White, 0);
            var amber = Read(channelValues, in layout, ChannelRole.Amber, 0);
            var uv = Read(channelValues, in layout, ChannelRole.UV, 0);
            color += new Rgb(white, white, white * 0.92) + new Rgb(amber, amber * 0.55, 0) + new Rgb(uv * 0.35, 0, uv);
        }
        else
        {
            var level = layout.Has(ChannelRole.Dimmer) ? 1 : Read(channelValues, in layout, ChannelRole.White, 0);
            color = Rgb.FromHex(fixture.GelColor) * level;
        }
        return new EmittedLight(
            (color * dimmer).Clamped(),
            Wide(channelValues, in layout, ChannelRole.Pan, ChannelRole.PanFine),
            Wide(channelValues, in layout, ChannelRole.Tilt, ChannelRole.TiltFine),
            Read(channelValues, in layout, ChannelRole.Strobe, 0),
            Read(channelValues, in layout, ChannelRole.Zoom, 0.5)
        );
    }

    private static double Read(ReadOnlySpan<byte> values, in ChannelLayout layout, ChannelRole role, double fallback)
    {
        var i = layout.IndexOf(role);
        return i >= 0 && i < values.Length ? values[i] / 255.0 : fallback;
    }

    private static double Wide(ReadOnlySpan<byte> values, in ChannelLayout layout, ChannelRole coarse, ChannelRole fine)
    {
        var c = layout.IndexOf(coarse);
        if (c < 0 || c >= values.Length)
            return 0.5;
        var f = layout.IndexOf(fine);
        return f >= 0 && f < values.Length ? (values[c] * 256 + values[f]) / 65535.0 : values[c] / 255.0;
    }
}
